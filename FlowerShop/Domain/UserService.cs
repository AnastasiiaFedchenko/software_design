using System;
using System.Diagnostics;
using Domain.InputPorts;
using Domain.OutputPorts;
using Microsoft.Extensions.Logging;

namespace Domain
{
    public class UserService : IUserService
    {
        private readonly IUserRepo _userRepo;
        private readonly ILogger<UserService> _logger;

        public UserService(IUserRepo userRepo, ILogger<UserService> logger)
        {
            _userRepo = userRepo ?? throw new ArgumentNullException(nameof(userRepo));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public UserType? CheckPasswordAndGetUserType(int id, string password)
        {
            using var activity = Diagnostics.ActivitySource.StartActivity("UserService.CheckPasswordAndGetUserType");
            activity?.SetTag("user.id", id);
            Diagnostics.RecordOperation("UserService.CheckPasswordAndGetUserType");

            _logger.LogInformation("Попытка аутентификации пользователя с ID: {UserId}", id);

            try
            {
                if (string.IsNullOrWhiteSpace(password))
                {
                    _logger.LogWarning("Пустой пароль для пользователя {UserId}", id);
                    return null;
                }

                var userType = _userRepo.CheckPasswordAndGetUserType(id, password);

                if (userType != null)
                    _logger.LogInformation("Успешная аутентификация пользователя {UserId} как {UserType}", id, userType.Value);
                else
                    _logger.LogWarning("Неудачная попытка аутентификации для пользователя {UserId}", id);

                return userType;
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                _logger.LogError(ex, "Ошибка при проверке учетных данных пользователя {UserId}", id);
                throw;
            }
        }

        public bool ChangePassword(int id, string currentPassword, string newPassword)
        {
            using var activity = Diagnostics.ActivitySource.StartActivity("UserService.ChangePassword");
            activity?.SetTag("user.id", id);
            Diagnostics.RecordOperation("UserService.ChangePassword");

            _logger.LogInformation("Запрос на смену пароля для пользователя {UserId}", id);

            if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
            {
                _logger.LogWarning("Пустой пароль при смене пароля пользователя {UserId}", id);
                return false;
            }

            if (currentPassword == newPassword)
            {
                _logger.LogWarning("Новый пароль совпадает с текущим для пользователя {UserId}", id);
                return false;
            }

            try
            {
                var result = _userRepo.ChangePassword(id, currentPassword, newPassword);
                if (result)
                {
                    _logger.LogInformation("Пароль пользователя {UserId} успешно изменен", id);
                }
                else
                {
                    _logger.LogWarning("Смена пароля пользователя {UserId} не выполнена", id);
                }
                return result;
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                _logger.LogError(ex, "Ошибка при смене пароля пользователя {UserId}", id);
                throw;
            }
        }
    }
}
