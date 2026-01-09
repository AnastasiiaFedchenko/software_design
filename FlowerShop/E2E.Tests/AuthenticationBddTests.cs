using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using LightBDD.Framework;
using LightBDD.Framework.Scenarios;
using LightBDD.XUnit2;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc.Testing;
using MimeKit;
using Npgsql;
using Xunit;

namespace E2E.Tests
{
    [Collection("Database collection")]
    [FeatureDescription("Authentication with 2FA and password rotation")]
    public class AuthenticationBddTests : FeatureFixture, IAsyncLifetime
    {
        private readonly DatabaseFixture _fixture;
        private HttpClient _client = null!;
        private int _userId;
        private string _password = string.Empty;
        private string _newPassword = string.Empty;
        private string _twoFactorCode = string.Empty;
        private string _recoveryCode = string.Empty;
        private string _baseUrl = "http://localhost:5000";
        private DateTimeOffset _runStartedAt;
        private DateTimeOffset _twoFactorRequestedAt;
        private DateTimeOffset _recoveryRequestedAt;
        private string? _imapUser;
        private string? _imapPassword;
        private string _imapHost = "imap.gmail.com";
        private int _imapPort = 993;
        private bool _imapUseSsl = true;
        private IConfiguration _config = null!;
        private WebApplicationFactory<Program>? _factory;
        private bool _useFactory;

        public AuthenticationBddTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        public Task InitializeAsync()
        {
            _runStartedAt = DateTimeOffset.UtcNow;
            _config = new ConfigurationBuilder()
                .AddUserSecrets<AuthenticationBddTests>(optional: true)
                .AddEnvironmentVariables()
                .Build();
            _useFactory = string.Equals(_config["E2E_USE_FACTORY"], "true", StringComparison.OrdinalIgnoreCase)
                || (string.IsNullOrEmpty(_config["BASE_URL"]) && string.IsNullOrEmpty(_config["CI"]));
            _baseUrl = _config["BASE_URL"] ?? _baseUrl;
            _userId = int.TryParse(_config["E2E_USER_ID"], out var id) ? id : 9001;
            _password = GetRequiredSetting("E2E_USER_PASSWORD");
            _newPassword = GetRequiredSetting("E2E_NEW_PASSWORD");
            _imapUser = _config["E2E_EMAIL_USER"];
            _imapPassword = _config["E2E_EMAIL_PASSWORD"];
            _imapHost = _config["E2E_EMAIL_HOST"] ?? _imapHost;
            _imapPort = int.TryParse(_config["E2E_EMAIL_PORT"], out var port) ? port : _imapPort;
            _imapUseSsl = !string.Equals(_config["E2E_EMAIL_SSL"], "false", StringComparison.OrdinalIgnoreCase);

            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                CookieContainer = new System.Net.CookieContainer()
            };
            if (_useFactory)
            {
                Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _fixture.TestConnectionString);
                Environment.SetEnvironmentVariable("TEST_CONNECTION_STRING", _fixture.TestConnectionString);
                Environment.SetEnvironmentVariable("AuthSettings__ShowTwoFactorCode", _config["AuthSettings:ShowTwoFactorCode"] ?? "true");
                Environment.SetEnvironmentVariable("AuthSettings__ShowRecoveryCode", _config["AuthSettings:ShowRecoveryCode"] ?? "true");
                if (!string.IsNullOrWhiteSpace(_config["SMTP_HOST"]))
                {
                    Environment.SetEnvironmentVariable("EmailSettings__SmtpHost", _config["SMTP_HOST"]);
                    Environment.SetEnvironmentVariable("EmailSettings__SmtpPort", _config["SMTP_PORT"]);
                    Environment.SetEnvironmentVariable("EmailSettings__SmtpUser", _config["SMTP_USER"]);
                    Environment.SetEnvironmentVariable("EmailSettings__SmtpPassword", _config["SMTP_PASSWORD"]);
                    Environment.SetEnvironmentVariable("EmailSettings__FromEmail", _config["SMTP_FROM"]);
                    Environment.SetEnvironmentVariable("EmailSettings__AdminEmail", _config["E2E_EMAIL_USER"]);
                }
                _factory = new TestWebAppFactory(_fixture.TestConnectionString, _config);
                _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
                {
                    AllowAutoRedirect = false
                });
            }
            else
            {
                _client = new HttpClient(handler) { BaseAddress = new Uri(_baseUrl) };
                WaitForServerAsync().GetAwaiter().GetResult();
            }

            EnsureUserExists();
            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            _client.Dispose();
            _factory?.Dispose();
            CleanupUser();
            return Task.CompletedTask;
        }

        [Scenario]
        public async Task TwoFactor_Login_Should_Work()
        {
            await Runner.RunScenarioAsync(
                given => Given_user_exists(),
                when => When_user_logs_in_with_password(),
                then => Then_two_factor_page_is_shown(),
                when => When_user_enters_two_factor_code(),
                then => Then_user_is_signed_in()
            );
        }

        [Scenario]
        public async Task Lockout_And_Recovery_Should_Work()
        {
            await Runner.RunScenarioAsync(
                given => Given_user_exists(),
                when => When_user_enters_wrong_password_three_times(),
                then => Then_account_is_locked_with_recovery_code(),
                when => When_user_recovers_access(),
                then => Then_user_can_login_after_recovery()
            );
        }

        [Scenario]
        public async Task Password_Change_Should_Work()
        {
            await Runner.RunScenarioAsync(
                given => Given_user_exists(),
                when => When_user_logs_in_with_password(),
                then => Then_two_factor_page_is_shown(),
                when => When_user_enters_two_factor_code(),
                then => Then_user_is_signed_in(),
                when => When_user_changes_password(),
                then => Then_user_can_login_with_new_password()
            );
        }

        private Task Given_user_exists()
        {
            EnsureUserExists();
            return Task.CompletedTask;
        }

        private async Task When_user_logs_in_with_password()
        {
            _twoFactorRequestedAt = DateTimeOffset.UtcNow;
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("id", _userId.ToString()),
                new KeyValuePair<string, string>("password", _password)
            });

            var response = await _client.PostAsync("/Account/Login", content);
            if (response.StatusCode != HttpStatusCode.Redirect)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(
                    $"Login expected Redirect, got {(int)response.StatusCode} {response.StatusCode}. Body:\n{body}");
            }
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/Account/TwoFactor", response.Headers.Location?.ToString() ?? "");
        }

        private async Task Then_two_factor_page_is_shown()
        {
            var response = await _client.GetAsync("/Account/TwoFactor");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            _twoFactorCode = ExtractCode(html, "twofactor-code");
            if (string.IsNullOrEmpty(_twoFactorCode))
            {
                _twoFactorCode = await WaitForEmailCodeAsync(
                    "2FA code",
                    $"User {_userId} 2FA code: (?<code>\\d+)",
                    _twoFactorRequestedAt);
            }
            Assert.False(string.IsNullOrEmpty(_twoFactorCode));
        }

        private async Task When_user_enters_two_factor_code()
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("code", _twoFactorCode)
            });

            var response = await _client.PostAsync("/Account/TwoFactor", content);
            if (response.StatusCode != HttpStatusCode.Redirect)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(
                    $"TwoFactor expected Redirect, got {(int)response.StatusCode} {response.StatusCode}. Body:\n{body}");
            }
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        private async Task Then_user_is_signed_in()
        {
            var response = await _client.GetAsync("/Admin/Index");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        private async Task When_user_enters_wrong_password_three_times()
        {
            for (int i = 0; i < 3; i++)
            {
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("id", _userId.ToString()),
                    new KeyValuePair<string, string>("password", "wrong")
                });
                var response = await _client.PostAsync("/Account/Login", content);
                if (i < 2)
                {
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                }
            }
        }

        private async Task Then_account_is_locked_with_recovery_code()
        {
            var response = await _client.GetAsync($"/Account/Login");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            _recoveryRequestedAt = DateTimeOffset.UtcNow;
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("id", _userId.ToString()),
                new KeyValuePair<string, string>("password", "wrong")
            });
            var lockResponse = await _client.PostAsync("/Account/Login", content);
            Assert.Equal(HttpStatusCode.OK, lockResponse.StatusCode);
            var html = await lockResponse.Content.ReadAsStringAsync();
            Assert.Contains("заблокирован", html, StringComparison.OrdinalIgnoreCase);
            _recoveryCode = ExtractCode(html, "recovery-code");
            if (string.IsNullOrEmpty(_recoveryCode))
            {
                _recoveryCode = await WaitForEmailCodeAsync(
                    "Recovery code",
                    $"User {_userId} recovery code: (?<code>\\d+)",
                    _recoveryRequestedAt);
            }
            Assert.False(string.IsNullOrEmpty(_recoveryCode));
        }

        private async Task When_user_recovers_access()
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("id", _userId.ToString()),
                new KeyValuePair<string, string>("recoveryCode", _recoveryCode)
            });
            var response = await _client.PostAsync("/Account/Recover", content);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        private async Task Then_user_can_login_after_recovery()
        {
            await When_user_logs_in_with_password();
            await Then_two_factor_page_is_shown();
            await When_user_enters_two_factor_code();
            await Then_user_is_signed_in();
        }

        private async Task When_user_changes_password()
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("currentPassword", _password),
                new KeyValuePair<string, string>("newPassword", _newPassword),
                new KeyValuePair<string, string>("confirmPassword", _newPassword)
            });

            var response = await _client.PostAsync("/Account/ChangePassword", content);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        private async Task Then_user_can_login_with_new_password()
        {
            await _client.GetAsync("/Account/Logout");

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("id", _userId.ToString()),
                new KeyValuePair<string, string>("password", _newPassword)
            });
            var response = await _client.PostAsync("/Account/Login", content);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/Account/TwoFactor", response.Headers.Location?.ToString() ?? "");
        }

        private void EnsureUserExists()
        {
            using var connection = new NpgsqlConnection(_fixture.TestConnectionString);
            connection.Open();

            using var deleteCmd = new NpgsqlCommand(@"DELETE FROM ""user"" WHERE id = @id", connection);
            deleteCmd.Parameters.AddWithValue("@id", _userId);
            deleteCmd.ExecuteNonQuery();

            using var insertCmd = new NpgsqlCommand(
                @"INSERT INTO ""user"" (id, name, type, password) VALUES (@id, @name, @type::user_role, @password)",
                connection);
            insertCmd.Parameters.AddWithValue("@id", _userId);
            insertCmd.Parameters.AddWithValue("@name", $"Tech User {_userId}");
            insertCmd.Parameters.AddWithValue("@type", "администратор");
            insertCmd.Parameters.AddWithValue("@password", _password);
            insertCmd.ExecuteNonQuery();
        }

        private void CleanupUser()
        {
            using var connection = new NpgsqlConnection(_fixture.TestConnectionString);
            connection.Open();

            using var deleteCmd = new NpgsqlCommand(@"DELETE FROM ""user"" WHERE id = @id", connection);
            deleteCmd.Parameters.AddWithValue("@id", _userId);
            deleteCmd.ExecuteNonQuery();
        }

        private static string ExtractCode(string html, string elementId)
        {
            var match = Regex.Match(html, $"id=\\\"{elementId}\\\">(?<code>\\d+)<");
            return match.Success ? match.Groups["code"].Value : string.Empty;
        }

        private async Task WaitForServerAsync()
        {
            var deadline = DateTimeOffset.UtcNow.AddSeconds(25);
            while (DateTimeOffset.UtcNow < deadline)
            {
                try
                {
                    using var response = await _client.GetAsync("/Account/Login");
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        return;
                    }
                }
                catch
                {
                    // ignore until timeout
                }
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
            throw new InvalidOperationException($"WebApp did not respond at {_baseUrl}. Start it or set E2E_USE_FACTORY=true.");
        }

        private async Task<string> WaitForEmailCodeAsync(string subjectContains, string bodyPattern, DateTimeOffset notBeforeUtc)
        {
            if (string.IsNullOrWhiteSpace(_imapUser) || string.IsNullOrWhiteSpace(_imapPassword))
            {
                throw new InvalidOperationException("IMAP credentials are required to read codes from email.");
            }

            var deadline = DateTimeOffset.UtcNow.AddSeconds(90);
            var regex = new Regex(bodyPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);

            while (DateTimeOffset.UtcNow < deadline)
            {
                var code = await TryReadLatestEmailCodeAsync(subjectContains, regex, notBeforeUtc);
                if (!string.IsNullOrEmpty(code))
                {
                    return code;
                }
                await Task.Delay(TimeSpan.FromSeconds(3));
            }

            return string.Empty;
        }

        private async Task<string> TryReadLatestEmailCodeAsync(string subjectContains, Regex regex, DateTimeOffset notBeforeUtc)
        {
            using var client = new ImapClient();
            await client.ConnectAsync(_imapHost, _imapPort, _imapUseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_imapUser, _imapPassword);

            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly);

            var query = SearchQuery.SubjectContains(subjectContains);
            var uids = await inbox.SearchAsync(query);
            var code = string.Empty;
            for (int i = uids.Count - 1; i >= 0; i--)
            {
                var message = await inbox.GetMessageAsync(uids[i]);
                if (message.Date.UtcDateTime < notBeforeUtc.UtcDateTime.AddMinutes(-2))
                {
                    continue;
                }

                var body = message.TextBody ?? message.HtmlBody ?? string.Empty;
                var match = regex.Match(body);
                if (match.Success)
                {
                    code = match.Groups["code"].Value;
                    break;
                }
            }

            await client.DisconnectAsync(true);
            return code;
        }

        private string GetRequiredSetting(string name)
        {
            var value = _config[name];
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Setting '{name}' is required for E2E tests.");
            }
            return value;
        }
    }
}
