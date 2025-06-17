using System;
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

            _logger.LogInformation("Инициализация UserService");
        }

        public UserType? CheckPasswordAndGetUserType(int id, string password)
        {
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
                _logger.LogError(ex, "Ошибка при проверке учетных данных пользователя {UserId}", id);
                throw;
            }
        }
    }
}