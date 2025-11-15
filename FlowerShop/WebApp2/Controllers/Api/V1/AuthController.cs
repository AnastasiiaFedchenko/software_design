using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using Domain.InputPorts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace WebApp2.Controllers.Api.V1
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IUserService userService, ILogger<AuthController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                var userType = _userService.CheckPasswordAndGetUserType(request.Id, request.Password);

                if (userType == null)
                {
                    _logger.LogWarning("Failed login attempt for user ID: {UserId}", request.Id);
                    return Unauthorized(new { message = "Неверные учетные данные" });
                }

                // Создаем claims для аутентификации
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, request.Id.ToString()),
                    new Claim(ClaimTypes.Role, userType.ToString()),
                    new Claim(ClaimTypes.Name, $"User_{request.Id}")
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
                };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                _logger.LogInformation("User {UserId} logged in as {Role}", request.Id, userType);

                return Ok(new LoginResponse
                {
                    UserId = request.Id,
                    Role = userType.ToString(),
                    Message = "Успешный вход"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for user {UserId}", request.Id);
                return StatusCode(500, new { message = "Ошибка при входе", error = ex.Message });
            }
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                _logger.LogInformation("User {UserId} logged out", userId);

                return Ok(new { message = "Успешный выход" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return StatusCode(500, new { message = "Ошибка при выходе", error = ex.Message });
            }
        }
    }

    public class LoginRequest
    {
        public int Id { get; set; }
        public string Password { get; set; }
    }

    public class LoginResponse
    {
        public int UserId { get; set; }
        public string Role { get; set; }
        public string Message { get; set; }
    }
}