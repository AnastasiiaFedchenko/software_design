using Domain;
using Domain.OutputPorts;
using InventoryOfProducts;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

public class InventoryRepoTests
{
    private readonly IInventoryRepo _inventoryRepo;
    private readonly NpgsqlConnection _connection;
    private readonly NpgsqlTransaction _transaction;
    private readonly ITestOutputHelper _output;

    public InventoryRepoTests(ITestOutputHelper output)
    {
        _output = output;
        _inventoryRepo = new InventoryRepo();
        _connection = new NpgsqlConnection("Host=127.0.0.1;Port=5432;Database=FlowerShopPPO;Username=postgres;Password=5432");
        _connection.Open();
        _transaction = _connection.BeginTransaction();
    }

    [Fact]
    public void GetAvailableProduct_ShouldReturnCorrectInventory()
    {
        // Act
        var inventory = _inventoryRepo.GetAvailableProduct(10, 0);
        _output.WriteLine($"Получено товаров: {inventory?.Products?.Count ?? 0}");

        // Assert
        Assert.NotNull(inventory);
        Assert.True(inventory.TotalAmount > 0, "Количество товаров должно быть больше 0");
        Assert.NotEmpty(inventory.Products);
    }

    [Fact]
    public void CheckNewAmount_ShouldReturnCorrectResults()
    {
        // Arrange
        const int productId = 70;
        var availableStock = GetProductStock(productId);
        _output.WriteLine($"Доступное количество товара ID {productId}: {availableStock}");

        // Act & Assert
        Assert.True(_inventoryRepo.CheckNewAmount(productId, availableStock - 1),
            $"Должно хватить 5 единиц (есть {availableStock})");

        Assert.False(_inventoryRepo.CheckNewAmount(productId, availableStock + 5),
            $"Не должно хватить 15 единиц (есть {availableStock})");
    }

    private int GetProductStock(int nomenclatureId)
    {
        using var cmd = new NpgsqlCommand(
                        @"
                        SELECT 
                            SUM(pis.amount) AS available_amount
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
                            bop.expiration_date > CURRENT_DATE and n.id = @nomenclatureId
                        GROUP BY 
                            n.id, n.name, p.selling_price, c.name
                        HAVING 
                            SUM(pis.amount) > 0
                        ORDER BY 
                            n.id
                        LIMIT 1",
            _connection, _transaction);

        cmd.Parameters.AddWithValue("@nomenclatureId", nomenclatureId);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }
}