using FlowerShop.Spa.Models;
using Microsoft.JSInterop;
using Domain.InputPorts;

namespace FlowerShop.Spa.Services;

public class AuthService
{
    private readonly IUserService _userService;
    private readonly IJSRuntime _jsRuntime;

    public AuthService(IUserService userService, IJSRuntime jsRuntime)
    {
        _userService = userService;
        _jsRuntime = jsRuntime;
    }

    public async Task<LoginResponse?> Login(LoginRequest request)
    {
        try
        {
            var userType = _userService.CheckPasswordAndGetUserType(request.UserId, request.Password);

            if (userType != null)
            {
                // Генерируем простой токен (в реальном приложении используй JWT)
                var token = $"token-{request.UserId}-{DateTime.Now.Ticks}";

                return new LoginResponse
                {
                    Token = token,
                    Role = userType.ToString(),
                    UserId = request.UserId
                };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Login error: {ex.Message}");
        }
        return null;
    }

    // Остальные методы без изменений...
    public async Task Logout()
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "jwtToken");
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "userRole");
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "userId");
    }

    public async Task<User?> GetCurrentUser()
    {
        var role = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "userRole");
        var userIdStr = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "userId");

        if (!string.IsNullOrEmpty(role) && int.TryParse(userIdStr, out int userId))
        {
            return new User { UserId = userId, Role = role };
        }
        return null;
    }

    public async Task<bool> IsAuthenticated()
    {
        var token = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "jwtToken");
        return !string.IsNullOrEmpty(token);
    }

    public async Task SaveUserData(LoginResponse userData)
    {
        if (!string.IsNullOrEmpty(userData.Token))
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "jwtToken", userData.Token);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "userRole", userData.Role);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "userId", userData.UserId.ToString());
        }
    }
}