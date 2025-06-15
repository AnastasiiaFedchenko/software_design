using Domain.OutputPorts;
using Domain;
using Microsoft.Extensions.Logging;
using Npgsql;
using System;
using System.Collections.Generic;

namespace ProductBatchLoading
{
    public class ProductBatchLoader : IProductBatchLoader
    {
        private readonly string _connectionString;
        private readonly ILogger<ProductBatchLoader> _logger;

        public ProductBatchLoader(string connectionString, ILogger<ProductBatchLoader> logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        public bool Load(ProductBatch batch)
        {
            if (batch == null || batch.ProductsInfo == null || batch.ProductsInfo.Count == 0)
            {
                _logger.LogWarning("Попытка загрузить пустую или невалидную партию товаров");
                return false;
            }

            _logger.LogInformation("Начало загрузки партии товаров ID: {BatchId}, товаров: {ProductsCount}",
                batch.Id, batch.ProductsInfo.Count);

            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();
                    _logger.LogInformation("Установлено соединение с БД");

                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // 1. Загрузка в batch_of_products
                            if (!InsertIntoBatchOfProducts(connection, transaction, batch))
                            {
                                _logger.LogError("Ошибка загрузки в batch_of_products");
                                transaction.Rollback();
                                return false;
                            }
                            _logger.LogInformation("Успешно загружено в batch_of_products");

                            // 2. Загрузка в product_in_stock
                            if (!InsertIntoProductInStock(connection, transaction, batch))
                            {
                                _logger.LogError("Ошибка загрузки в product_in_stock");
                                transaction.Rollback();
                                return false;
                            }
                            _logger.LogInformation("Успешно загружено в product_in_stock");

                            // 3. Загрузка в price
                            if (!InsertIntoPrice(connection, transaction, batch))
                            {
                                _logger.LogError("Ошибка загрузки в price");
                                transaction.Rollback();
                                return false;
                            }
                            _logger.LogInformation("Успешно загружено в price");

                            transaction.Commit();
                            _logger.LogInformation("Транзакция успешно завершена. Партия ID: {BatchId} загружена", batch.Id);
                            return true;
                        }
                        catch (NpgsqlException ex)
                        {
                            _logger.LogError(ex, "Ошибка БД при загрузке партии. Транзакция откачена");
                            transaction.Rollback();
                            return false;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Неожиданная ошибка при загрузке партии. Транзакция откачена");
                            transaction.Rollback();
                            return false;
                        }
                    }
                }
            }
            catch (NpgsqlException ex)
            {
                _logger.LogError(ex, "Ошибка подключения к БД");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неожиданная ошибка при подключении к БД");
                return false;
            }
        }

        private bool InsertIntoBatchOfProducts(NpgsqlConnection connection, NpgsqlTransaction transaction, ProductBatch batch)
        {
            _logger.LogInformation("Начало загрузки в batch_of_products");

            foreach (var product in batch.ProductsInfo)
            {
                // Валидация данных
                if (product.IdNomenclature <= 0 || product.Amount <= 0 || product.CostPrice <= 0)
                {
                    _logger.LogError("Невалидные данные товара: ID {ProductId}, Amount {Amount}, CostPrice {CostPrice}",
                        product.IdNomenclature, product.Amount, product.CostPrice);
                    return false;
                }

                if (product.ProductionDate > product.ExpirationDate)
                {
                    _logger.LogError("Дата производства позже срока годности: {ProductionDate} > {ExpirationDate}",
                        product.ProductionDate, product.ExpirationDate);
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

                    int affectedRows = command.ExecuteNonQuery();
                    if (affectedRows != 1)
                    {
                        _logger.LogError("Не удалось вставить запись в batch_of_products для товара {ProductId}",
                            product.IdNomenclature);
                        return false;
                    }
                }
            }
            return true;
        }

        private bool InsertIntoProductInStock(NpgsqlConnection connection, NpgsqlTransaction transaction, ProductBatch batch)
        {
            _logger.LogDebug("Начало загрузки в product_in_stock");

            try
            {
                int nextId = GetNextProductInStockId(connection, transaction);

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
                            _logger.LogError("Не удалось вставить/обновить запись в product_in_stock для товара {ProductId}",
                                product.IdNomenclature);
                            return false;
                        }
                    }
                }
                return true;
            }
            catch (NpgsqlException ex)
            {
                _logger.LogError(ex, "Ошибка БД при загрузке в product_in_stock");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неожиданная ошибка при загрузке в product_in_stock");
                return false;
            }
        }

        private bool InsertIntoPrice(NpgsqlConnection connection, NpgsqlTransaction transaction, ProductBatch batch)
        {
            _logger.LogInformation("Начало загрузки в price");

            try
            {
                foreach (var product in batch.ProductsInfo)
                {
                    var sql = @"
                        WITH avg_cost_by_year AS (
                            SELECT 
                                id_nomenclature,
                                EXTRACT(YEAR FROM production_date) AS production_year,
                                AVG(cost_price) * 1.55 AS calculated_price
                            FROM 
                                batch_of_products
                            WHERE 
                                id_nomenclature = @nomenclatureId
                            GROUP BY 
                                id_nomenclature, 
                                EXTRACT(YEAR FROM production_date)
                        )
                        INSERT INTO price (id, id_nomenclature, selling_price, id_product_batch)
                        SELECT 
                        ROW_NUMBER() OVER () + COALESCE((SELECT MAX(id) FROM price), 0),
		                    bop.id_nomenclature,
		                    ac.calculated_price,
		                    bop.id_product_batch
		                FROM 
		                    batch_of_products bop 
		                JOIN 
		                    avg_cost_by_year ac ON bop.id_nomenclature = ac.id_nomenclature 
		                    AND EXTRACT(YEAR FROM bop.production_date) = ac.production_year
		                where bop.id_nomenclature = @nomenclatureId and bop.id_product_batch = @batchId";

                    _logger.LogTrace("Выполнение SQL: {Sql}", sql);

                    using (var command = new NpgsqlCommand(sql, connection, transaction))
                    {
                        command.Parameters.AddWithValue("@nomenclatureId", product.IdNomenclature);
                        command.Parameters.AddWithValue("@batchId", batch.Id);

                        int affectedRows = command.ExecuteNonQuery();
                        if (affectedRows != 1)
                        {
                            _logger.LogError("Не удалось вставить запись в price для товара {ProductId}",
                                product.IdNomenclature);
                            return false;
                        }
                    }
                }
                return true;
            }
            catch (NpgsqlException ex)
            {
                _logger.LogError(ex, "Ошибка БД при загрузке в price");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неожиданная ошибка при загрузке в price");
                return false;
            }
        }

        private int GetNextProductInStockId(NpgsqlConnection connection, NpgsqlTransaction transaction)
        {
            var sql = "SELECT COALESCE(MAX(id), 0) FROM product_in_stock";
            _logger.LogTrace("Получение следующего ID для product_in_stock: {Sql}", sql);

            using (var command = new NpgsqlCommand(sql, connection, transaction))
            {
                object result = command.ExecuteScalar();
                int nextId = Convert.ToInt32(result) + 1;
                _logger.LogInformation("Получен следующий ID: {NextId}", nextId);
                return nextId;
            }
        }
    }
}