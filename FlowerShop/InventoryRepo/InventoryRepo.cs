using System;
using System.Collections.Generic;
using System.Data;
using Domain;
using Domain.OutputPorts;
using ConnectionToDB;

namespace InventoryOfProducts
{
    public class InventoryRepo : IInventoryRepo
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public InventoryRepo(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public Inventory GetAvailableProduct(int limit, int skip)
        {
            using var connection = _connectionFactory.CreateOpenConnection();

            var query = @"
                SELECT 
                    n.id AS nomenclature_id,
                    n.name AS nomenclature_name,
                    p.selling_price AS price,
                    SUM(pis.amount) AS available_amount,
                    c.name AS country_name
                FROM 
                    nomenclature n
                JOIN 
                    price p ON n.id = p.id_nomenclature
                JOIN 
                    product_in_stock pis ON p.id_nomenclature = pis.id_nomenclature 
                                        AND p.id_product_batch = pis.id_product_batch
                JOIN
                    batch_of_products bop ON pis.id_product_batch = bop.id_product_batch
                                        AND pis.id_nomenclature = bop.id_nomenclature
                JOIN
                    country c ON n.country_id = c.id
                WHERE
                    bop.expiration_date > CURRENT_DATE
                GROUP BY 
                    n.id, n.name, p.selling_price, c.name
                HAVING 
                    SUM(pis.amount) > 0
                ORDER BY 
                    n.id
                LIMIT @limit OFFSET @skip";

            using var command = connection.CreateCommand();
            command.CommandText = query;
            AddParameter(command, "@limit", limit);
            AddParameter(command, "@skip", skip);

            var productLines = new List<ProductLine>();
            int totalAmount = 0;

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var product = new Product(
                        id_nomenclature: reader.GetInt32(0),
                        price: reader.GetDouble(2),
                        amount_in_stock: reader.GetInt32(3),
                        type: reader.GetString(1),
                        country: reader.GetString(4)
                    );

                    productLines.Add(new ProductLine(product, reader.GetInt32(3)));
                    totalAmount += 1;
                }
            }

            return new Inventory(
                id: Guid.NewGuid(),
                date: DateTime.Now,
                total_amount: totalAmount,
                products: productLines
            );
        }

        public Product GetInfoOnProduct(int productID)
        {
            if (productID <= 0)
                throw new ArgumentException("Id товара не может быть отрицательным или равным нулю.");

            using var connection = _connectionFactory.CreateOpenConnection();

            var query = @"
                SELECT 
                    n.id AS nomenclature_id,
                    n.name AS nomenclature_name,
                    p.selling_price AS price,
                    SUM(pis.amount) AS available_amount,
                    c.name AS country_name
                FROM 
                    nomenclature n
                JOIN 
                    price p ON n.id = p.id_nomenclature
                JOIN 
                    product_in_stock pis ON p.id_nomenclature = pis.id_nomenclature 
                                        AND p.id_product_batch = pis.id_product_batch
                JOIN
                    batch_of_products bop ON pis.id_product_batch = bop.id_product_batch
                                        AND pis.id_nomenclature = bop.id_nomenclature
                JOIN
                    country c ON n.country_id = c.id
                WHERE
                    bop.expiration_date > CURRENT_DATE 
                    AND n.id = @product_id
                GROUP BY 
                    n.id, n.name, p.selling_price, c.name
                HAVING 
                    SUM(pis.amount) > 0
                ORDER BY 
                    n.id
                LIMIT 1";

            using var command = connection.CreateCommand();
            command.CommandText = query;
            AddParameter(command, "@product_id", productID);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                var product = new Product(
                    id_nomenclature: reader.GetInt32(0),
                    price: reader.GetDouble(2),
                    amount_in_stock: reader.GetInt32(3),
                    type: reader.GetString(1),
                    country: reader.GetString(4)
                );

                return product;
            }

            return null;
        }

        public bool CheckNewAmount(int product_id, int new_n)
        {
            if (new_n <= 0)
                throw new ArgumentException("Новое количество не может быть отрицательным или равным нулю.");

            if (product_id <= 0)
                throw new ArgumentException("Id товара не может быть отрицательным или равным нулю.");

            using var connection = _connectionFactory.CreateOpenConnection();

            var query = @"
                SELECT COALESCE(SUM(pis.amount), 0)
                FROM product_in_stock pis
                JOIN batch_of_products bop ON pis.id_product_batch = bop.id_product_batch 
                                         AND pis.id_nomenclature = bop.id_nomenclature
                JOIN nomenclature n ON pis.id_nomenclature = n.id
                WHERE n.id = @product_id
                AND bop.expiration_date > CURRENT_DATE";

            using var command = connection.CreateCommand();
            command.CommandText = query;
            AddParameter(command, "@product_id", product_id);

            long availableAmount = Convert.ToInt64(command.ExecuteScalar() ?? 0L);

            return new_n <= availableAmount;
        }

        // Вспомогательный метод для добавления параметров
        private void AddParameter(IDbCommand command, string parameterName, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = parameterName;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    }
}