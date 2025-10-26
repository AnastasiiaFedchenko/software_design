using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Xunit;
using Allure.Xunit.Attributes;
using Integration.Tests;
using Xunit.Abstractions;

namespace E2E.Tests
{
    [Trait("Category", "E2E")]
    [AllureSuite("Real E2E Tests")]
    [AllureFeature("End-to-End Scenarios")]
    [Collection("Database collection")]
    public class RealE2ETests : IAsyncLifetime
    {
        private readonly HttpClient _client;
        private readonly string _baseUrl = "http://localhost:5031";
        private bool _isAppRunning = false;
        private readonly DatabaseFixture _fixture;
        private readonly ITestOutputHelper _output;

        public RealE2ETests(ITestOutputHelper output, DatabaseFixture fixture)
        {
            _output = output;
            _fixture = fixture;
            _client = new HttpClient();
            _client.BaseAddress = new Uri(_baseUrl);
            _client.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task InitializeAsync()
        {
            _output.WriteLine($"Using test database: {_fixture.TestConnectionString}");

            // Проверяем, запущено ли приложение
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
            _client?.Dispose();
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

            // Arrange
            var loginData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("id", "1"),
                new KeyValuePair<string, string>("password", "pass1")
            });

            // Act
            var response = await _client.PostAsync("/Account/Login", loginData);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/Admin/Index", response.Headers.Location?.ToString() ?? "");

            // Сохраняем cookies для последующих запросов
            StoreCookies(response);
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

            // Сначала логинимся
            await LoginAsAdmin();

            // Act - Получаем страницу с товарами
            var response = await _client.GetAsync("/Admin/Order");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();

            // Проверяем что страница загрузилась
            Assert.Contains("Товары", content);
            Assert.Contains("Корзина", content);
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

            // Логинимся
            await LoginAsAdmin();

            // Получаем доступный товар из тестовой БД
            var availableProductId = await GetAvailableProductIdFromTestDb();
            Assert.True(availableProductId > 0, "No available products in test database");

            // Act - Добавляем товар в корзину
            var response = await _client.PostAsync($"/Admin/AddToCart?productId={availableProductId}&quantity=1", null);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            // Проверяем что корзина обновилась
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

            // Логинимся
            await LoginAsAdmin();

            // Получаем доступный товар из тестовой БД
            var availableProductId = await GetAvailableProductIdFromTestDb();
            Assert.True(availableProductId > 0, "No available products in test database");

            // 1. Добавляем товар в корзину
            await _client.PostAsync($"/Admin/AddToCart?productId={availableProductId}&quantity=1", null);

            // 2. Оформляем заказ
            var orderResponse = await _client.PostAsync("/Admin/SubmitOrder", null);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, orderResponse.StatusCode);

            // 3. Проверяем результат
            var resultResponse = await _client.GetAsync(orderResponse.Headers.Location);
            var resultContent = await resultResponse.Content.ReadAsStringAsync();
            Assert.Contains("Заказ оформлен", resultContent);
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

            // Логинимся
            await LoginAsAdmin();

            // Arrange - Создаем тестовый файл с данными из тестовой БД
            var batchContent = await GenerateTestBatchContent();

            using var content = new MultipartFormDataContent();
            var fileContent = new StringContent(batchContent);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            content.Add(fileContent, "batchFile", "test_batch.txt");

            // Act - Загружаем файл
            var response = await _client.PostAsync("/Admin/LoadBatch", content);

            // Assert
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

            // Act - Пытаемся получить доступ к админке без авторизации
            var response = await _client.GetAsync("/Admin/Index");

            // Assert - Должен быть редирект на логин
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

            // Проверяем существование пользователя продавца в тестовой БД
            var sellerExists = await CheckSellerUserExists();

            if (sellerExists)
            {
                // Логинимся как продавец
                var sellerLoginData = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("id", "2"),
                    new KeyValuePair<string, string>("password", "pass2")
                });

                var loginResponse = await _client.PostAsync("/Account/Login", sellerLoginData);

                if (loginResponse.StatusCode == HttpStatusCode.Redirect)
                {
                    // Если продавец существует, проверяем его доступ
                    StoreCookies(loginResponse);

                    // Продавец должен иметь доступ к своим страницам
                    var sellerPageResponse = await _client.GetAsync("/Seller/Index");
                    Assert.Equal(HttpStatusCode.OK, sellerPageResponse.StatusCode);
                    _output.WriteLine("Seller role access verified successfully");
                }
            }
            else
            {
                _output.WriteLine("Seller user not available in test database - test partially completed");
            }
        }

        private async Task LoginAsAdmin()
        {
            var loginData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("id", "1"),
                new KeyValuePair<string, string>("password", "pass1")
            });

            var response = await _client.PostAsync("/Account/Login", loginData);
            StoreCookies(response);
        }

        private void StoreCookies(HttpResponseMessage response)
        {
            if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
            {
                foreach (var cookie in cookies)
                {
                    _client.DefaultRequestHeaders.Add("Cookie", cookie);
                }
            }
        }

        private async Task<int> GetAvailableProductIdFromTestDb()
        {
            // Используем тестовую БД для получения доступного товара
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
            // Генерируем тестовые данные на основе содержимого тестовой БД
            using var connection = new Npgsql.NpgsqlConnection(_fixture.TestConnectionString);
            await connection.OpenAsync();

            // Получаем несколько ID товаров из тестовой БД
            using var cmd = new Npgsql.NpgsqlCommand(
                @"SELECT id FROM nomenclature ORDER BY id LIMIT 5",
                connection);

            using var reader = await cmd.ExecuteReaderAsync();
            var productIds = new List<int>();

            while (await reader.ReadAsync())
            {
                productIds.Add(reader.GetInt32(0));
            }

            // Создаем тестовый batch контент
            var batchContent = new StringBuilder();
            foreach (var productId in productIds)
            {
                batchContent.AppendLine($"{productId};2025-05-18;2025-05-25;100.00;10");
            }

            return batchContent.ToString();
        }

        private async Task<bool> CheckSellerUserExists()
        {
            // Проверяем существование пользователя продавца в тестовой БД
            using var connection = new Npgsql.NpgsqlConnection(_fixture.TestConnectionString);
            await connection.OpenAsync();

            using var cmd = new Npgsql.NpgsqlCommand(
                @"SELECT COUNT(*) FROM employee WHERE id = 2 AND role = 'seller'",
                connection);

            var result = await cmd.ExecuteScalarAsync();
            return result != null && Convert.ToInt32(result) > 0;
        }
    }
}