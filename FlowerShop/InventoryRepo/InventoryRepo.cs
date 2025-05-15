using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Domain;
using Domain.OutputPorts;
using Npgsql;

namespace InventoryOfProducts
{
    public class InventoryRepo: IInventoryRepo
    {
        public Inventory GetAvailableProduct(int limit, int skip)
        {
            // Создаем подключение к базе данных
            using var connection = new NpgsqlConnection("Host=127.0.0.1;Port=5432;Database=FlowerShop;Username=postgres;Password=5432");
            connection.Open();

            // Запрос для получения доступных продуктов с их номенклатурой, ценами и странами
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
                            bop.expiration_date > CURRENT_DATE  -- Проверка на срок годности
                        GROUP BY 
                            n.id, n.name, p.selling_price, c.name
                        HAVING 
                            SUM(pis.amount) > 0
                        ORDER BY 
                            n.id
                        LIMIT @limit OFFSET @skip";

            var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@limit", limit);
            command.Parameters.AddWithValue("@skip", skip);

            var productLines = new List<ProductLine>();
            int totalAmount = 0;

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var product = new Product(
                        nomenclature: reader.GetInt32(0),
                        amount: reader.GetInt32(3),    // available_amount
                        price: reader.GetDouble(2),   // price
                        amount_in_stock: reader.GetInt32(3), // available_amount
                        type: reader.GetString(1),     // nomenclature_name
                        country: reader.GetString(4)   // country_name
                    );

                    productLines.Add(new ProductLine(product, reader.GetInt32(3)));
                    totalAmount += 1;
                }
            }

            // Получаем информацию о пользователе
            var userQuery = "SELECT id, name FROM \"user\" LIMIT 1";
            var userCommand = new NpgsqlCommand(userQuery, connection);
            var userReader = userCommand.ExecuteReader();

            User responsibleUser = new User(0, "Unknown");
            if (userReader.Read())
            {
                responsibleUser = new User(
                    0,
                    userReader.GetString(1)
                );
            }

            return new Inventory(
                id: Guid.NewGuid(),
                date: DateTime.Now,
                supplier: responsibleUser,
                responsible: responsibleUser,
                total_amount: totalAmount,
                products: productLines
            );
        }

        public Product GetInfoOnProduct(int productID)
        {
            // Create database connection
            using var connection = new NpgsqlConnection("Host=127.0.0.1;Port=5432;Database=FlowerShop;Username=postgres;Password=5432");
            connection.Open();

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
                            bop.expiration_date > CURRENT_DATE and n.id = @product_id
                        GROUP BY 
                            n.id, n.name, p.selling_price, c.name
                        HAVING 
                            SUM(pis.amount) > 0
                        ORDER BY 
                            n.id
                        LIMIT 1";

            var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@product_id", productID);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                var product = new Product(
                    nomenclature: reader.GetInt32(0),
                    amount: 0,  // Default amount, can be set later
                    price: reader.GetDouble(2),
                    amount_in_stock: reader.GetInt32(3),
                    type: reader.GetString(1),
                    country: reader.GetString(4)
                );

                return product;
            }

            // Return null if product not found
            return null;
        }
        public bool CheckNewAmount(int product_id, int new_n)
        {
            // Создаем подключение к базе данных
            using var connection = new NpgsqlConnection("Host=127.0.0.1;Port=5432;Database=FlowerShop;Username=postgres;Password=5432");
            connection.Open();

            // Запрос для получения суммы доступного количества товара в годных партиях
            var query = @"
                        SELECT COALESCE(SUM(pis.amount), 0)
                        FROM product_in_stock pis
                        JOIN batch_of_products bop ON pis.id_product_batch = bop.id_product_batch 
                                                 AND pis.id_nomenclature = bop.id_nomenclature
                        JOIN nomenclature n ON pis.id_nomenclature = n.id
                        WHERE n.id = @product_id::integer
                        AND bop.expiration_date > CURRENT_DATE";

            var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@product_id", product_id.ToString());

            long availableAmount = (long)(command.ExecuteScalar() ?? 0L);

            // Проверяем, что запрашиваемое количество не превышает доступное
            return new_n <= availableAmount;
        }
    }
}
