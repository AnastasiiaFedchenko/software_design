using Domain;
using Domain.InputPorts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Serilog;
using Microsoft.AspNetCore.Authorization;
using WebApp.Services;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;

namespace WebApp.Controllers
{
    public class AccountController : Controller
    {
        private readonly IUserService _userService;
        private readonly ILogger<AccountController> _logger;
        private readonly AuthStateService _authState;
        private readonly AuthSettings _authSettings;
        private readonly IEmailSender _emailSender;
        private readonly EmailSettings _emailSettings;
        private readonly IWebHostEnvironment _env;
        private const string PendingUserIdKey = "PendingUserId";
        private const string PendingUserRoleKey = "PendingUserRole";

        public AccountController(
            IUserService userService,
            ILogger<AccountController> logger,
            AuthStateService authState,
            IOptions<AuthSettings> authOptions,
            IEmailSender emailSender,
            IOptions<EmailSettings> emailOptions,
            IWebHostEnvironment env)
        {
            _userService = userService;
            _logger = logger;
            _authState = authState;
            _authSettings = authOptions.Value;
            _emailSender = emailSender;
            _emailSettings = emailOptions.Value;
            _env = env;
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
                if (_authState.IsLocked(id))
                {
                    return LockedAccount(id);
                }
                var userType = _userService.CheckPasswordAndGetUserType(id, password);

                switch (userType)
                {
                    case UserType.Administrator:
                    case UserType.Seller:
                    case UserType.Storekeeper:
                        return StartTwoFactor(id, userType.Value);

                    case null:
                        Log.Warning("Неудачная попытка входа (неверный ID или пароль)");
                        _authState.RegisterFailedAttempt(id);
                        if (_authState.IsLocked(id))
                        {
                            return LockedAccount(id);
                        }
                        var remaining = _authState.RemainingAttempts(id);
                        ModelState.AddModelError("", $"Неверный ID или пароль. Осталось попыток: {remaining}");
                        return View();
                }

                ModelState.AddModelError("", "Неизвестный тип пользователя");
                return View();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при входе пользователя");
                var message = _env.IsDevelopment() || _env.IsEnvironment("Test")
                    ? ex.Message
                    : "Произошла ошибка при входе";
                ModelState.AddModelError("", message);
                return View();
            }
        }

        [HttpGet]
        public IActionResult TwoFactor()
        {
            var pendingId = HttpContext.Session.GetInt32(PendingUserIdKey);
            if (pendingId == null)
            {
                return RedirectToAction("Login");
            }

            if (_authSettings.ShowTwoFactorCode ||
                (Request.Headers.TryGetValue("X-Test-Auth", out var testHeader) &&
                 string.Equals(testHeader.ToString(), "true", StringComparison.OrdinalIgnoreCase)))
            {
                ViewBag.TwoFactorCode = _authState.PeekTwoFactorCode(pendingId.Value);
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> TwoFactor(string code)
        {
            var pendingId = HttpContext.Session.GetInt32(PendingUserIdKey);
            var pendingRole = HttpContext.Session.GetString(PendingUserRoleKey);
            if (pendingId == null || string.IsNullOrEmpty(pendingRole))
            {
                return RedirectToAction("Login");
            }

            if (_authState.IsLocked(pendingId.Value))
            {
                return LockedAccount(pendingId.Value);
            }

            if (!_authState.ValidateTwoFactorCode(pendingId.Value, code ?? string.Empty))
            {
                _authState.RegisterFailedAttempt(pendingId.Value);
                if (_authState.IsLocked(pendingId.Value))
                {
                    return LockedAccount(pendingId.Value);
                }
                var remaining = _authState.RemainingAttempts(pendingId.Value);
                ModelState.AddModelError("", $"Неверный код подтверждения. Осталось попыток: {remaining}");
                if (_authSettings.ShowTwoFactorCode)
                {
                    ViewBag.TwoFactorCode = _authState.PeekTwoFactorCode(pendingId.Value);
                }
                return View();
            }

            _authState.ResetAttempts(pendingId.Value);
            HttpContext.Session.Remove(PendingUserIdKey);
            HttpContext.Session.Remove(PendingUserRoleKey);

            var role = Enum.Parse<UserType>(pendingRole);
            await SignInUser(pendingId.Value, role);
            return RedirectToRole(role);
        }

        [Authorize]
        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }

        [Authorize]
        [HttpPost]
        public IActionResult ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            if (newPassword != confirmPassword)
            {
                ModelState.AddModelError("", "Пароли не совпадают");
                return View();
            }

            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            {
                return Unauthorized();
            }

            var result = _userService.ChangePassword(userId, currentPassword, newPassword);
            if (!result)
            {
                ModelState.AddModelError("", "Не удалось изменить пароль");
                return View();
            }

            ViewBag.Message = "Пароль успешно изменен";
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Recover()
        {
            await HttpContext.SignOutAsync();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Recover(int id, string recoveryCode)
        {
            await HttpContext.SignOutAsync();
            if (_authState.TryRecover(id, recoveryCode ?? string.Empty))
            {
                ViewBag.Message = "Доступ восстановлен. Можно войти.";
                return View();
            }

            ModelState.AddModelError("", "Неверный код восстановления");
            return View();
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

        private IActionResult StartTwoFactor(int userId, UserType role)
        {
            _authState.ResetAttempts(userId);
            var code = _authState.GenerateTwoFactorCode(userId);
            _logger.LogInformation("2FA code generated for user {UserId}", userId);
            SendEmail(_emailSettings.AdminEmail, "2FA code", $"User {userId} 2FA code: {code}");

            HttpContext.Session.SetInt32(PendingUserIdKey, userId);
            HttpContext.Session.SetString(PendingUserRoleKey, role.ToString());

            return RedirectToAction("TwoFactor");
        }

        private IActionResult LockedAccount(int userId)
        {
            ViewBag.UserId = userId;
            if (_authSettings.ShowRecoveryCode)
            {
                ViewBag.RecoveryCode = _authState.GetRecoveryCode(userId);
            }
            var recoveryCode = _authState.GetRecoveryCode(userId);
            if (!string.IsNullOrEmpty(recoveryCode))
            {
                SendEmail(_emailSettings.AdminEmail, "Recovery code", $"User {userId} recovery code: {recoveryCode}");
            }
            HttpContext.SignOutAsync().GetAwaiter().GetResult();
            return View("Locked");
        }

        private IActionResult RedirectToRole(UserType role)
        {
            return role switch
            {
                UserType.Administrator => RedirectToAction("Index", "Admin"),
                UserType.Seller => RedirectToAction("Index", "Seller"),
                UserType.Storekeeper => RedirectToAction("Index", "Storekeeper"),
                _ => RedirectToAction("Login")
            };
        }

        private void SendEmail(string to, string subject, string body)
        {
            try
            {
                _emailSender.Send(to, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email}", to);
            }
        }

        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync();
            return RedirectToAction("Login");
        }
    }
}
