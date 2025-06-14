using System;
using System.Collections.Generic;
using System.Data;
using Domain;
using Domain.OutputPorts;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace UserValidation
{
    public class UserRepo : IUserRepo
    {
        private readonly string _connectionString;
        private readonly ILogger<UserRepo> _logger;

        public UserRepo(string connectionString, ILogger<UserRepo> logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        public UserType? CheckPasswordAndGetUserType(int id, string inputPassword)
        {
            try
            {
                _logger.LogInformation("Попытка авторизации пользователя {UserId}", id);

                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();
                    _logger.LogInformation("Установлено соединение с БД");

                    var query = @"
                    SELECT type 
                    FROM ""user"" 
                    WHERE id = @id AND password = @password;";

                    using (var command = new NpgsqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@id", id);
                        command.Parameters.AddWithValue("@password", inputPassword);

                        using (var reader = command.ExecuteReader())
                        {
                            if (!reader.Read())
                            {
                                _logger.LogWarning("Неверные учетные данные для пользователя {UserId}", id);
                                return null;
                            }

                            string userTypeFromDb = reader.GetString(0);
                            _logger.LogDebug("Получена роль из БД: {UserRole}", userTypeFromDb);

                            return userTypeFromDb switch
                            {
                                "администратор" => UserType.Administrator,
                                "продавец" => UserType.Seller,
                                "кладовщик" => UserType.Storekeeper,
                                _ => null // Неизвестная роль
                            };
                        }
                    }
                }
            }
            catch (NpgsqlException ex)
            {
                _logger.LogError(ex, "Ошибка базы данных при проверке пользователя {UserId}", id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неожиданная ошибка при проверке пользователя {UserId}", id);
                throw;
            }
        }
    }
}