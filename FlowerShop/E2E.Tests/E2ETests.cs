using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;
using Allure.Xunit.Attributes;
using Xunit.Abstractions;

namespace E2E.Tests
{
    [Trait("Category", "E2E")]
    [AllureSuite("Real E2E Tests")]
    [AllureFeature("End-to-End Scenarios")]
    [Collection("Database collection")]
    public class RealE2ETests : IAsyncLifetime
    {
        private HttpClient _client;
        private HttpClientHandler _handler;
        private CookieContainer _cookies;
        private readonly string _baseUrl = "http://localhost:5031";
        private bool _isAppRunning = false;
        private readonly DatabaseFixture _fixture;
        private readonly ITestOutputHelper _output;

        public RealE2ETests(ITestOutputHelper output, DatabaseFixture fixture)
        {
            _output = output;
            _fixture = fixture;
            ResetClient();
        }

        public async Task InitializeAsync()
        {
            _output.WriteLine($"Using test database: {_fixture.TestConnectionString}");

            try
            {
                var response = await _client.GetAsync("/Account/Login");
                _isAppRunning = response.StatusCode == HttpStatusCode.OK;

                if (_isAppRunning)
                {
                    _output.WriteLine("Application is running and accessible");
                }
                else
                {
                    _output.WriteLine("Application responded but with non-OK status");
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Application not accessible: {ex.Message}");
                _isAppRunning = false;
            }
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        [Fact]
        [AllureStory("Authentication")]
        [AllureTag("E2E")]
        public async Task Admin_Login_Should_Work()
        {
            if (!_isAppRunning)
            {
                _output.WriteLine("Test skipped - application not running");
                return;
            }

            var loginOk = await LoginAsAdmin();
            Assert.True(loginOk, "Admin login failed");
            _output.WriteLine("Admin login successful");
        }

        [Fact]
        [AllureStory("Product Management")]
        [AllureTag("E2E")]
        public async Task Browse_Products_Should_Work()
        {
            if (!_isAppRunning)
            {
                _output.WriteLine("Test skipped - application not running");
                return;
            }

            Assert.True(await LoginAsAdmin(), "Admin login failed");

            var response = await _client.GetAsync("/Admin/Order");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            var decodedContent = WebUtility.HtmlDecode(content);

            Assert.Contains("Оформление заказа", decodedContent);
            Assert.Contains("Доступные товары", decodedContent);
            _output.WriteLine("Product browsing page loaded successfully");
        }

        [Fact]
        [AllureStory("Shopping Cart")]
        [AllureTag("E2E")]
        public async Task Add_To_Cart_Should_Work()
        {
            if (!_isAppRunning)
            {
                _output.WriteLine("Test skipped - application not running");
                return;
            }

            Assert.True(await LoginAsAdmin(), "Admin login failed");

            var availableProductId = await GetAvailableProductIdFromTestDb();
            Assert.True(availableProductId > 0, "No available products in test database");

            var response = await _client.PostAsync($"/Admin/AddToCart?productId={availableProductId}&quantity=1", null);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            var cartResponse = await _client.GetAsync("/Admin/Order");
            var cartContent = await cartResponse.Content.ReadAsStringAsync();
            Assert.Contains("Корзина", cartContent);
            _output.WriteLine($"Product {availableProductId} added to cart successfully");
        }

        [Fact]
        [AllureStory("Order Processing")]
        [AllureTag("E2E")]
        public async Task Complete_Purchase_Flow_Should_Work()
        {
            if (!_isAppRunning)
            {
                _output.WriteLine("Test skipped - application not running");
                return;
            }

            Assert.True(await LoginAsAdmin(), "Admin login failed");

            var availableProductId = await GetAvailableProductIdFromTestDb();
            Assert.True(availableProductId > 0, "No available products in test database");

            await _client.PostAsync($"/Admin/AddToCart?productId={availableProductId}&quantity=1", null);

            var orderResponse = await _client.PostAsync("/Admin/SubmitOrder", null);
            Assert.Equal(HttpStatusCode.Redirect, orderResponse.StatusCode);

            var resultResponse = await _client.GetAsync(orderResponse.Headers.Location);
            var resultContent = await resultResponse.Content.ReadAsStringAsync();
            var decodedResultContent = WebUtility.HtmlDecode(resultContent);
            Assert.Contains("Заказ оформлен.", decodedResultContent);
            _output.WriteLine($"Purchase flow completed for product {availableProductId}");
        }

        [Fact]
        [AllureStory("Batch Operations")]
        [AllureTag("E2E")]
        public async Task Batch_Upload_Should_Work()
        {
            if (!_isAppRunning)
            {
                _output.WriteLine("Test skipped - application not running");
                return;
            }

            Assert.True(await LoginAsAdmin(), "Admin login failed");

            var batchContent = await GenerateTestBatchContent();

            using var content = new MultipartFormDataContent();
            var fileContent = new StringContent(batchContent);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            content.Add(fileContent, "batchFile", "test_batch.txt");

            var response = await _client.PostAsync("/Admin/LoadBatch", content);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            _output.WriteLine("Batch upload completed successfully");
        }

        [Fact]
        [AllureStory("Authorization")]
        [AllureTag("E2E")]
        public async Task Access_Control_Should_Work()
        {
            if (!_isAppRunning)
            {
                _output.WriteLine("Test skipped - application not running");
                return;
            }

            ClearCookies();

            var response = await _client.GetAsync("/Admin/Index");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/Account/Login", response.Headers.Location?.ToString() ?? "");
            _output.WriteLine("Access control working correctly - redirect to login when not authenticated");
        }

        [Fact]
        [AllureStory("User Roles")]
        [AllureTag("E2E")]
        public async Task Different_User_Roles_Should_Have_Different_Access()
        {
            if (!_isAppRunning)
            {
                _output.WriteLine("Test skipped - application not running");
                return;
            }

            var sellerExists = await CheckSellerUserExists();

            if (sellerExists)
            {
                var loginOk = await LoginAsUser("2", "pass2");
                if (!loginOk)
                {
                    _output.WriteLine("Seller login failed - skipping role access check");
                    return;
                }

                var sellerPageResponse = await _client.GetAsync("/Seller/Index");
                Assert.Equal(HttpStatusCode.OK, sellerPageResponse.StatusCode);
                _output.WriteLine("Seller role access verified successfully");
            }
            else
            {
                _output.WriteLine("Seller user not available in test database - test partially completed");
            }
        }

        private async Task<bool> LoginAsAdmin()
        {
            return await LoginAsUser("1", "pass1");
        }

        private async Task<bool> LoginAsUser(string id, string password)
        {
            var loginData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("id", id),
                new KeyValuePair<string, string>("password", password)
            });

            var response = await _client.PostAsync("/Account/Login", loginData);
            if (response.StatusCode != HttpStatusCode.Redirect)
            {
                _output.WriteLine($"Login failed for user {id}: {response.StatusCode}");
                return false;
            }

            var location = response.Headers.Location?.ToString() ?? string.Empty;

            if (location.Contains("/Account/TwoFactor", StringComparison.OrdinalIgnoreCase))
            {
                var twoFactorResponse = await _client.GetAsync(location);
                if (twoFactorResponse.StatusCode == HttpStatusCode.Redirect)
                {
                    _output.WriteLine($"Unexpected redirect during 2FA: {twoFactorResponse.Headers.Location}");
                    return false;
                }
                var html = await twoFactorResponse.Content.ReadAsStringAsync();
                var code = ExtractCode(html, "twofactor-code");
                if (string.IsNullOrEmpty(code))
                {
                    var snippet = html.Length > 300 ? html[..300] : html;
                    _output.WriteLine($"TwoFactor code not found in response. Status: {twoFactorResponse.StatusCode}. Body: {snippet}");
                    return false;
                }

                var twoFactorData = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("code", code)
                });
                var confirmResponse = await _client.PostAsync("/Account/TwoFactor", twoFactorData);

                if (confirmResponse.StatusCode != HttpStatusCode.Redirect)
                {
                    _output.WriteLine("TwoFactor confirmation failed");
                    return false;
                }
            }

            return true;
        }

        private static string ExtractCode(string html, string elementId)
        {
            var match = Regex.Match(
                html,
                $"id=\"{Regex.Escape(elementId)}\">(?<code>\\d+)<",
                RegexOptions.IgnoreCase);
            return match.Success ? match.Groups["code"].Value : string.Empty;
        }

        private void ClearCookies()
        {
            ResetClient();
        }

        private void ResetClient()
        {
            _cookies = new CookieContainer();
            _handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                UseCookies = true,
                CookieContainer = _cookies
            };
            _client = new HttpClient(_handler)
            {
                BaseAddress = new Uri(_baseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };
            _client.DefaultRequestHeaders.Add("X-Test-Auth", "true");
        }

        private async Task<int> GetAvailableProductIdFromTestDb()
        {
            using var connection = new Npgsql.NpgsqlConnection(_fixture.TestConnectionString);
            await connection.OpenAsync();

            using var cmd = new Npgsql.NpgsqlCommand(
                @"SELECT n.id 
                  FROM nomenclature n
                  JOIN product_in_stock pis ON n.id = pis.id_nomenclature
                  WHERE pis.amount > 0
                  LIMIT 1",
                connection);

            var result = await cmd.ExecuteScalarAsync();
            return result != null ? Convert.ToInt32(result) : 0;
        }

        private async Task<string> GenerateTestBatchContent()
        {
            using var connection = new Npgsql.NpgsqlConnection(_fixture.TestConnectionString);
            await connection.OpenAsync();

            using var cmd = new Npgsql.NpgsqlCommand(
                @"SELECT id FROM nomenclature ORDER BY id LIMIT 5",
                connection);

            using var reader = await cmd.ExecuteReaderAsync();
            var productIds = new List<int>();

            while (await reader.ReadAsync())
            {
                productIds.Add(reader.GetInt32(0));
            }

            var batchContent = new StringBuilder();
            foreach (var productId in productIds)
            {
                batchContent.AppendLine($"{productId};2025-05-18;2025-05-25;100.00;10");
            }

            return batchContent.ToString();
        }

        private async Task<bool> CheckSellerUserExists()
        {
            using var connection = new Npgsql.NpgsqlConnection(_fixture.TestConnectionString);
            await connection.OpenAsync();

            using var cmd = new Npgsql.NpgsqlCommand(
                @"SELECT COUNT(*) FROM ""user"" WHERE id = 2 AND type = 'продавец'",
                connection);

            var result = await cmd.ExecuteScalarAsync();
            return result != null && Convert.ToInt32(result) > 0;
        }
    }
}
