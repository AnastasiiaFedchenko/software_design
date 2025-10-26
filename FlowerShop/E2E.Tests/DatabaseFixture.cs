using System;
using System.IO;
using Npgsql;
using Xunit;

namespace E2E.Tests
{
    [CollectionDefinition("DatabaseIntegrationTests", DisableParallelization = true)]
    public class DatabaseFixture : IDisposable
    {
        private const string TestDbName = "flowershoptest";
        private const string MasterConnectionString = "Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres;Password=5432;Include Error Detail=true";

        public string TestConnectionString { get; } =
            $"Host=127.0.0.1;Port=5432;Database={TestDbName};Username=postgres;Password=5432;Include Error Detail=true";

        public NpgsqlConnection Db { get; private set; }

        public DatabaseFixture()
        {
            // Create test database
            using var masterConnection = new NpgsqlConnection(MasterConnectionString);
            masterConnection.Open();

            try
            {
                using (var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS {TestDbName}", masterConnection))
                {
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = new NpgsqlCommand($"CREATE DATABASE {TestDbName}", masterConnection))
                {
                    cmd.ExecuteNonQuery();
                }
            }
            finally
            {
                masterConnection.Close();
            }

            // Initialize schema and test data
            Db = new NpgsqlConnection(TestConnectionString);
            Db.Open();

            // Получаем директорию где находится исполняемый файл
            var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);

            // Поднимаемся на 3 уровня вверх чтобы найти корень проекта
            var projectRoot = Directory.GetParent(assemblyDirectory)?.Parent?.Parent;
            var sqlPath = Path.Combine(projectRoot.FullName, "E2E.Tests", "CreationOfTestDB.sql");

            if (!File.Exists(sqlPath))
            {
                // Альтернативный способ - поиск от текущей директории
                sqlPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "CreationOfTestDB.sql");
                sqlPath = Path.GetFullPath(sqlPath);
            }

            if (!File.Exists(sqlPath))
            {
                throw new FileNotFoundException($"Could not find SQL file at: {sqlPath}");
            }

            var createScript = File.ReadAllText(sqlPath);
            using (var cmd = new NpgsqlCommand(createScript, Db))
            {
                cmd.ExecuteNonQuery();
            }
        }

        public void Dispose()
        {
            Db?.Close();
            Db?.Dispose();

            // Drop test database
            using var masterConnection = new NpgsqlConnection(MasterConnectionString);
            masterConnection.Open();

            try
            {
                // Force disconnect all connections to the test database
                using (var cmd = new NpgsqlCommand(
                    $"SELECT pg_terminate_backend(pg_stat_activity.pid) FROM pg_stat_activity " +
                    $"WHERE pg_stat_activity.datname = '{TestDbName}' AND pid <> pg_backend_pid()",
                    masterConnection))
                {
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS {TestDbName}", masterConnection))
                {
                    cmd.ExecuteNonQuery();
                }
            }
            finally
            {
                masterConnection.Close();
            }
        }
    }

    [CollectionDefinition("Database collection")]
    public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
    {
    }
}