using System;
using System.Data;
using Domain;
using Domain.OutputPorts;
using Npgsql;

namespace ConnectionToDB
{
    public interface IDbConnectionFactory
    {
        IDbConnection CreateOpenConnection();
    }

    public class NpgsqlConnectionFactory : IDbConnectionFactory
    {
        private readonly string _connectionString;

        public NpgsqlConnectionFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        public IDbConnection CreateOpenConnection()
        {
            var connection = new NpgsqlConnection(_connectionString);
            connection.Open();
            return connection;
        }
    }
}