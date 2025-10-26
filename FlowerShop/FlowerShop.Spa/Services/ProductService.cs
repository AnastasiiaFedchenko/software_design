using FlowerShop.Spa.Models;
using Domain.InputPorts;

namespace FlowerShop.Spa.Services;

public class ProductServiceHere
{
    private readonly IProductService _domainProductService;

    public ProductServiceHere(IProductService domainProductService)
    {
        _domainProductService = domainProductService;
    }

    public async Task<Inventory?> GetProducts(int skip = 0, int limit = 20)
    {
        try
        {
            // Используем доменный сервис напрямую
            var products = _domainProductService.GetAllAvailableProducts(limit, skip);
            return MapToSpaInventory(products);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting products: {ex.Message}");
            return null;
        }
    }

    public async Task<Product?> GetProductInfo(int productId)
    {
        try
        {
            var product = _domainProductService.GetInfoOnProduct(productId);
            return MapToSpaProduct(product);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting product info: {ex.Message}");
            return null;
        }
    }

    private Inventory MapToSpaInventory(Domain.Inventory domainInventory)
    {
        return new Inventory
        {
            Products = domainInventory.Products.Select(p => new InventoryItem
            {
                Product = MapToSpaProduct(p.Product),
                Amount = p.Amount
            }).ToList(),
            TotalAmount = domainInventory.TotalAmount
        };
    }

    private Product MapToSpaProduct(Domain.Product domainProduct)
    {
        return new Product
        {
            IdNomenclature = domainProduct.IdNomenclature,
            Type = domainProduct.Type,
            Country = domainProduct.Country,
            Price = domainProduct.Price,
            AmountInStock = domainProduct.AmountInStock
        };
    }
}