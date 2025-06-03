using System;
using System.Collections.Generic;
using System.Data;
using Domain;
using Domain.OutputPorts;
using Npgsql;

namespace UserValidation
{
    public class UserRepo : IUserRepo
    {
        private readonly string _connectionString;

        public UserRepo()
        {
            _connectionString = "Host=127.0.0.1;Port=5432;Database=FlowerShopPPO;Username=postgres;Password=5432";
        }

        public UserType? CheckPasswordAndGetUserType(int id, string inputPassword)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

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
    }
}