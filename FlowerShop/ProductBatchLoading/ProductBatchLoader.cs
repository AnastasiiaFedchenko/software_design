using Domain.OutputPorts;
using System;
using System.Collections.Generic;
using Npgsql;
using Domain;

namespace ProductBatchLoading
{
    public class ProductBatchLoader : IProductBatchLoader
    {
        public bool Load(ProductBatch batch)
        {
            if (batch == null || batch.ProductsInfo == null || batch.ProductsInfo.Count == 0)
            {
                return false;
            }

            try
            {
                using (var connection = new NpgsqlConnection("Host=127.0.0.1;Port=5432;Database=FlowerShopPPO;Username=postgres;Password=5432"))
                {
                    connection.Open();

                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // 1. Сначала загружаем данные в batch_of_products
                            if (!InsertIntoBatchOfProducts(connection, transaction, batch))
                            {
                                transaction.Rollback();
                                return false;
                            }
                            Console.WriteLine("загружено в batch_of_products");

                            // 2. Затем загружаем данные в product_in_stock
                            if (!InsertIntoProductInStock(connection, transaction, batch))
                            {
                                transaction.Rollback();
                                return false;
                            }
                            Console.WriteLine("загружено в product_in_stock");

                            transaction.Commit();
                            return true;
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            Console.WriteLine($"Ошибка при загрузке партии: {ex.Message}");
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка подключения: {ex.Message}");
                return false;
            }
        }

        private bool InsertIntoBatchOfProducts(NpgsqlConnection connection, NpgsqlTransaction transaction, ProductBatch batch)
        {
            foreach (var product in batch.ProductsInfo)
            {
                // Валидация данных
                if (product.IdNomenclature <= 0 || product.Amount <= 0 || product.CostPrice <= 0)
                {
                    return false;
                }

                if (product.ProductionDate > product.ExpirationDate)
                {
                    return false;
                }

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

                    if (command.ExecuteNonQuery() != 1)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private bool InsertIntoProductInStock(NpgsqlConnection connection, NpgsqlTransaction transaction, ProductBatch batch)
        {
            try
            {
                // Сначала получаем максимальный ID для генерации новых
                int nextId = GetNextProductInStockId(connection, transaction);
                Console.WriteLine($"Next product in stock id {nextId}");

                foreach (var product in batch.ProductsInfo)
                {
                    var sql = @"
                        INSERT INTO product_in_stock (
                            id,
                            id_nomenclature,
                            id_product_batch,
                            amount,
                            storage_place
                        )
                        SELECT
                            @id,
                            @nomenclatureId,
                            @batchId,
                            @amount,
                            @storage_place
                        ON CONFLICT (id_nomenclature, id_product_batch) 
                        DO UPDATE SET 
                            amount = product_in_stock.amount + @amount";

                    using (var command = new NpgsqlCommand(sql, connection, transaction))
                    {
                        command.Parameters.AddWithValue("@id", nextId++);
                        command.Parameters.AddWithValue("@nomenclatureId", product.IdNomenclature);
                        command.Parameters.AddWithValue("@batchId", batch.Id);
                        command.Parameters.AddWithValue("@amount", product.Amount);
                        command.Parameters.AddWithValue("@storage_place", product.StoragePlace);

                        int affectedRows = command.ExecuteNonQuery();
                        if (affectedRows != 1)
                        {
                            Console.WriteLine($"Не удалось вставить запись для номенклатуры {product.IdNomenclature}");
                            return false;
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при добавлении в product_in_stock: {ex.Message}");
                return false;
            }
        }

        private int GetNextProductInStockId(NpgsqlConnection connection, NpgsqlTransaction transaction)
        {
            var sql = "SELECT COALESCE(MAX(id), 0) FROM product_in_stock";
            using (var command = new NpgsqlCommand(sql, connection, transaction))
            {
                object result = command.ExecuteScalar();
                return Convert.ToInt32(result) + 1;
            }
        }
    }
}