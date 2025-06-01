using Domain.OutputPorts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Domain;
using Domain.OutputPorts;
using Npgsql;

namespace ProductBatchLoading
{
    public class ProductBatchLoader: IProductBatchLoader
    {
        private readonly string _connectionString;
        public ProductBatchLoader(string connectionString) 
        {
            _connectionString = connectionString;
        }

        public bool load(ProductBatch batch)
        {
            // Проверка входных данных
            if (batch == null || batch.ProductsInfo == null || batch.ProductsInfo.Count == 0)
            {
                return false;
            }

            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();

                    // Начинаем транзакцию для атомарности операций
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            foreach (var product in batch.ProductsInfo)
                            {
                                // Проверяем валидность данных перед вставкой
                                if (product.IdNomenclature <= 0 || product.Amount <= 0 || product.CostPrice <= 0)
                                {
                                    transaction.Rollback();
                                    return false;
                                }

                                // Проверяем, что дата производства не позже даты истечения срока
                                if (product.ProductionDate > product.ExpirationDate)
                                {
                                    transaction.Rollback();
                                    return false;
                                }

                                // SQL запрос для вставки данных
                                var sql = @"
                                            INSERT INTO batch_of_products (
                                                id_product_batch, 
                                                id_nomenclature, 
                                                production_date, 
                                                expiration_date, 
                                                cost_price, 
                                                amount, 
                                                responsible, 
                                                suppliers
                                            ) 
                                            VALUES (
                                                @batchId, 
                                                @nomenclatureId, 
                                                @productionDate, 
                                                @expirationDate, 
                                                @costPrice, 
                                                @amount, 
                                                @responsible, 
                                                @supplier
                                            )";

                                using (var command = new NpgsqlCommand(sql, connection, transaction))
                                {
                                    command.Parameters.AddWithValue("@batchId", batch.Id);
                                    command.Parameters.AddWithValue("@nomenclatureId", product.IdNomenclature);
                                    command.Parameters.AddWithValue("@productionDate", product.ProductionDate);
                                    command.Parameters.AddWithValue("@expirationDate", product.ExpirationDate);
                                    command.Parameters.AddWithValue("@costPrice", (decimal)product.CostPrice);
                                    command.Parameters.AddWithValue("@amount", product.Amount);
                                    command.Parameters.AddWithValue("@responsible", batch.Responsible);
                                    command.Parameters.AddWithValue("@supplier", batch.Supplier);

                                    int affectedRows = command.ExecuteNonQuery();
                                    if (affectedRows != 1)
                                    {
                                        transaction.Rollback();
                                        return false;
                                    }
                                }
                            }

                            // Если все вставки прошли успешно, коммитим транзакцию
                            transaction.Commit();
                            return true;
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw; // Перебрасываем исключение для обработки выше
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Логирование ошибки (можно добавить)
                Console.WriteLine($"Ошибка при загрузке партии: {ex.Message}");
                return false;
            }
        }
    }
}
