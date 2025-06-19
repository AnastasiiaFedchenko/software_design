using Domain;
using Domain.InputPorts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Serilog;
using Microsoft.AspNetCore.Authorization;

namespace WebApp.Controllers
{
    public class AccountController : Controller
    {
        private readonly IUserService _userService;
        private readonly ILogger<AccountController> _logger;
        private readonly IConfiguration _config;

        public AccountController(
            IUserService userService,
            ILogger<AccountController> logger,
            IConfiguration config)
        {
            _userService = userService;
            _logger = logger;
            _config = config;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(int id, string password)
        {
            try
            {
                var defaultLimit = _config.GetValue<int>("AppSettings:DefaultPaginationLimit");
                var userType = _userService.CheckPasswordAndGetUserType(id, password);

                switch (userType)
                {
                    case UserType.Administrator:
                        Log.Information("Пользователь {UserId} вошёл как администратор", id);
                        await SignInUser(id, userType);
                        return RedirectToAction("Index", "Admin");

                    case UserType.Seller:
                        Log.Information("Пользователь {UserId} вошёл как продавец", id);
                        await SignInUser(id, userType);
                        return RedirectToAction("Index", "Seller");

                    case UserType.Storekeeper:
                        Log.Information("Пользователь {UserId} вошёл как кладовщик", id);
                        await SignInUser(id, userType);
                        return RedirectToAction("Index", "Storekeeper");

                    case null:
                        Log.Warning("Неудачная попытка входа (неверный ID или пароль)");
                        ModelState.AddModelError("", "Неверный ID или пароль");
                        return View();
                }

                ModelState.AddModelError("", "Неизвестный тип пользователя");
                return View();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при входе пользователя");
                ModelState.AddModelError("", "Произошла ошибка при входе");
                return View();
            }
        }

        private async Task SignInUser(int userId, UserType? userType)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, userType.ToString())
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(principal);
        }

        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync();
            return RedirectToAction("Login");
        }
    }
}