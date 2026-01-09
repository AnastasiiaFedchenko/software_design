using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using Domain;
using Domain.OutputPorts;
using ConnectionToDB;
using Npgsql;

namespace UserValidation
{
    public class UserRepo : IUserRepo
    {
        private readonly IDbConnectionFactory _connectionFactory;

        // Внедряем фабрику
        public UserRepo(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public UserType? CheckPasswordAndGetUserType(int id, string inputPassword)
        {
            using (var connection = _connectionFactory.CreateOpenConnection())
            {
                var query = @"
                SELECT type::text 
                FROM ""user"" 
                WHERE id = @id AND password = @password;";

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = query;

                    // Создаем параметры через CreateParameter
                    var idParam = command.CreateParameter();
                    idParam.ParameterName = "@id";
                    idParam.Value = id;
                    command.Parameters.Add(idParam);

                    var passwordParam = command.CreateParameter();
                    passwordParam.ParameterName = "@password";
                    passwordParam.Value = inputPassword;
                    command.Parameters.Add(passwordParam);

                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                            return null; // Пользователь не найден или пароль неверный

                        string userTypeFromDb = reader.GetString(0); // Роль из БД

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

        public bool ChangePassword(int id, string currentPassword, string newPassword)
        {
            using (var connection = _connectionFactory.CreateOpenConnection())
            {
                var query = @"
                UPDATE ""user""
                SET password = @newPassword
                WHERE id = @id AND password = @currentPassword;";

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = query;

                    var idParam = command.CreateParameter();
                    idParam.ParameterName = "@id";
                    idParam.Value = id;
                    command.Parameters.Add(idParam);

                    var currentParam = command.CreateParameter();
                    currentParam.ParameterName = "@currentPassword";
                    currentParam.Value = currentPassword;
                    command.Parameters.Add(currentParam);

                    var newParam = command.CreateParameter();
                    newParam.ParameterName = "@newPassword";
                    newParam.Value = newPassword;
                    command.Parameters.Add(newParam);

                    var rows = command.ExecuteNonQuery();
                    return rows > 0;
                }
            }
        }
    }
}
