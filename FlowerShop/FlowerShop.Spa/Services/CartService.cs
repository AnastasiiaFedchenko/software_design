using FlowerShop.Spa.Models;
using Domain.InputPorts;

namespace FlowerShop.Spa.Services;

public class CartService
{
    private readonly IProductService _domainProductService;
    private List<ReceiptLine> _cart = new List<ReceiptLine>();

    public CartService(IProductService domainProductService)
    {
        _domainProductService = domainProductService;
    }

    public async Task<List<ReceiptLine>?> GetCart()
    {
        return _cart;
    }

    public async Task<bool> AddToCart(int productId, int quantity)
    {
        try
        {
            var product = _domainProductService.GetInfoOnProduct(productId);
            if (product == null) return false;

            var existingItem = _cart.FirstOrDefault(x => x.Product.IdNomenclature == productId);
            if (existingItem != null)
            {
                _cart.Remove(existingItem);
                _cart.Add(new ReceiptLine(
                    MapToSpaProduct(product),
                    existingItem.Amount + quantity
                ));
            }
            else
            {
                _cart.Add(new ReceiptLine(
                    MapToSpaProduct(product),
                    quantity
                ));
            }
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error adding to cart: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> RemoveFromCart(int productId)
    {
        try
        {
            var itemToRemove = _cart.FirstOrDefault(x => x.Product.IdNomenclature == productId);
            if (itemToRemove != null)
            {
                _cart.Remove(itemToRemove);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error removing from cart: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> SubmitOrder()
    {
        try
        {
            if (!_cart.Any()) return false;

            // Преобразуем корзину в доменные объекты
            var domainCart = _cart.Select(item => new Domain.ReceiptLine(
                new Domain.Product(
                    item.Product.IdNomenclature,
                    item.Product.Price,
                    item.Product.AmountInStock,
                    item.Product.Type,
                    item.Product.Country
                ),
                item.Amount
            )).ToList();

            // TODO: Нужен ID пользователя из текущей сессии
            int customerId = 1; // Временное значение

            var receipt = _domainProductService.MakePurchase(domainCart, customerId);

            if (receipt.Id != -1)
            {
                _cart.Clear();
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error submitting order: {ex.Message}");
            return false;
        }
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