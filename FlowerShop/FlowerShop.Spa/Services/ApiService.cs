using System.Net.Http.Headers;
using System.Text.Json;
using FlowerShop.Spa.Models;


namespace FlowerShop.Spa.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl = "https://localhost:7036/api/v1";

        public ApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri(_baseUrl);
        }

        private async Task AddAuthHeader()
        {
            var token = await GetToken();
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
            }
        }

        private async Task<string?> GetToken()
        {
            return await _localStorage.GetItemAsync<string>("jwtToken");
        }

        // Методы API будут здесь
    }
}
