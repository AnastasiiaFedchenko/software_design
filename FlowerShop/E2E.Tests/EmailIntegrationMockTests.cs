using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using MimeKit;
using Npgsql;
using Xunit;
using MockSmtpServer;

namespace E2E.Tests;

[Trait("Category", "E2E")]
[Collection("Database collection")]
public class EmailIntegrationMockTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private MockSmtpServerHost? _smtpServer;
    private WebApplicationFactory<Program>? _factory;
    private HttpClient _client = null!;
    private string _outputDir = string.Empty;

    private const int UserId = 9001;
    private const string Password = "mockpass1";

    public EmailIntegrationMockTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _outputDir = Path.Combine(Path.GetTempPath(), "flowershop-mock-smtp", Guid.NewGuid().ToString("N"));
        var port = GetFreePort();
        _smtpServer = MockSmtpServerHost.Start("127.0.0.1", port, _outputDir);
        WaitForSmtpAsync(port, TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();

        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _fixture.TestConnectionString);
        Environment.SetEnvironmentVariable("TEST_CONNECTION_STRING", _fixture.TestConnectionString);

        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = _fixture.TestConnectionString,
            ["TEST_CONNECTION_STRING"] = _fixture.TestConnectionString,
            ["AuthSettings:ShowTwoFactorCode"] = "false",
            ["AuthSettings:ShowRecoveryCode"] = "false",
            ["EmailSettings:SmtpHost"] = "127.0.0.1",
            ["EmailSettings:SmtpPort"] = port.ToString(),
            ["EmailSettings:SmtpUser"] = "",
            ["EmailSettings:SmtpPassword"] = "",
            ["EmailSettings:FromEmail"] = "noreply@local.test",
            ["EmailSettings:AdminEmail"] = "admin@local.test",
            ["EmailSettings:UseSsl"] = "false"
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        _factory = new TestWebAppFactory(_fixture.TestConnectionString, config);
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        EnsureUserExists();
        EnsureUserHasPassword();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        _factory?.Dispose();
        if (_smtpServer != null)
        {
            await _smtpServer.DisposeAsync();
        }
        CleanupUser();
    }

    [Fact]
    public async Task TwoFactor_Email_Should_Be_Sent_Via_Mock_Smtp()
    {
        var notBefore = DateTimeOffset.UtcNow;
        var loginData = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("id", UserId.ToString()),
            new KeyValuePair<string, string>("password", Password)
        });

        var response = await _client.PostAsync("/Account/Login", loginData);
        if (response.StatusCode != HttpStatusCode.Redirect)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Login expected Redirect, got {(int)response.StatusCode} {response.StatusCode}. Body:\n{body}");
        }
        Assert.Contains("/Account/TwoFactor", response.Headers.Location?.ToString() ?? "");

        var email = await WaitForEmailAsync("2FA code", notBefore, TimeSpan.FromSeconds(15));
        var code = ExtractCodeFromEmail(email, @"User \d+ 2FA code: (?<code>\d+)");

        Assert.False(string.IsNullOrWhiteSpace(code));

        var twoFactorData = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("code", code)
        });
        var twoFactorResponse = await _client.PostAsync("/Account/TwoFactor", twoFactorData);
        Assert.Equal(HttpStatusCode.Redirect, twoFactorResponse.StatusCode);
    }

    private async Task<MimeMessage> WaitForEmailAsync(string subjectContains, DateTimeOffset notBeforeUtc, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        Directory.CreateDirectory(_outputDir);

        while (DateTimeOffset.UtcNow < deadline)
        {
            var files = Directory.GetFiles(_outputDir, "*.eml");
            foreach (var file in files.OrderByDescending(File.GetCreationTimeUtc))
            {
                var created = File.GetCreationTimeUtc(file);
                if (created < notBeforeUtc.UtcDateTime.AddSeconds(-2))
                {
                    continue;
                }

                var message = MimeMessage.Load(file);
                if (message.Subject != null &&
                    message.Subject.Contains(subjectContains, StringComparison.OrdinalIgnoreCase))
                {
                    return message;
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(300));
        }

        throw new TimeoutException($"Email with subject '{subjectContains}' not found in {_outputDir}.");
    }

    private static string ExtractCodeFromEmail(MimeMessage message, string pattern)
    {
        var body = message.TextBody ?? message.HtmlBody ?? string.Empty;
        var match = Regex.Match(body, pattern, RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["code"].Value : string.Empty;
    }

    private void EnsureUserExists()
    {
        using var connection = new NpgsqlConnection(_fixture.TestConnectionString);
        connection.Open();

        using var deleteCmd = new NpgsqlCommand(@"DELETE FROM ""user"" WHERE id = @id", connection);
        deleteCmd.Parameters.AddWithValue("@id", UserId);
        deleteCmd.ExecuteNonQuery();

        using var insertCmd = new NpgsqlCommand(
            @"INSERT INTO ""user"" (id, name, type, password) VALUES (@id, @name, @type::user_role, @password)",
            connection);
        insertCmd.Parameters.AddWithValue("@id", UserId);
        insertCmd.Parameters.AddWithValue("@name", $"Mock User {UserId}");
        insertCmd.Parameters.AddWithValue("@type", "администратор");
        insertCmd.Parameters.AddWithValue("@password", Password);
        insertCmd.ExecuteNonQuery();
    }

    private void EnsureUserHasPassword()
    {
        using var connection = new NpgsqlConnection(_fixture.TestConnectionString);
        connection.Open();

        using var cmd = new NpgsqlCommand(
            @"SELECT COUNT(*) FROM ""user"" WHERE id = @id AND password = @password",
            connection);
        cmd.Parameters.AddWithValue("@id", UserId);
        cmd.Parameters.AddWithValue("@password", Password);
        var result = cmd.ExecuteScalar();
        if (result == null || Convert.ToInt32(result) == 0)
        {
            throw new InvalidOperationException($"Test user {UserId} with password was not created in test DB.");
        }
    }

    private void CleanupUser()
    {
        using var connection = new NpgsqlConnection(_fixture.TestConnectionString);
        connection.Open();

        using var deleteCmd = new NpgsqlCommand(@"DELETE FROM ""user"" WHERE id = @id", connection);
        deleteCmd.Parameters.AddWithValue("@id", UserId);
        deleteCmd.ExecuteNonQuery();
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task WaitForSmtpAsync(int port, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, port);
                return;
            }
            catch
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200));
            }
        }

        throw new TimeoutException($"Mock SMTP did not start on port {port}.");
    }
}
