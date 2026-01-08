using WebApp2.Application.DTOs;
using WebApp2.Controllers.Api.V1;

namespace WebApp2.Services
{
    public interface ICartStorageService
    {
        List<CartItem> GetCart(int userId);
        void AddToCart(int userId, CartItem item);
        void UpdateCartItem(int userId, int productId, int quantity);
        void RemoveFromCart(int userId, int productId);
        void ClearCart(int userId);
        bool CartExists(int userId);
    }

    public class CartStorageService : ICartStorageService
    {
        private static readonly Dictionary<int, List<CartItem>> _userCarts = new();
        private static readonly object _sync = new();

        public List<CartItem> GetCart(int userId)
        {
            lock (_sync)
            {
                return _userCarts.ContainsKey(userId)
                    ? new List<CartItem>(_userCarts[userId])
                    : new List<CartItem>();
            }
        }

        public void AddToCart(int userId, CartItem item)
        {
            lock (_sync)
            {
                if (!_userCarts.ContainsKey(userId))
                {
                    _userCarts[userId] = new List<CartItem>();
                }

                var existingItem = _userCarts[userId].FirstOrDefault(x => x.ProductId == item.ProductId);
                if (existingItem != null)
                {
                    existingItem.Quantity += item.Quantity;
                }
                else
                {
                    _userCarts[userId].Add(item);
                }
            }
        }

        public void UpdateCartItem(int userId, int productId, int quantity)
        {
            lock (_sync)
            {
                if (_userCarts.ContainsKey(userId))
                {
                    var existingItem = _userCarts[userId].FirstOrDefault(x => x.ProductId == productId);
                    if (existingItem != null)
                    {
                        existingItem.Quantity = quantity;
                    }
                }
            }
        }

        public void RemoveFromCart(int userId, int productId)
        {
            lock (_sync)
            {
                if (_userCarts.ContainsKey(userId))
                {
                    var itemToRemove = _userCarts[userId].FirstOrDefault(x => x.ProductId == productId);
                    if (itemToRemove != null)
                    {
                        _userCarts[userId].Remove(itemToRemove);
                    }
                }
            }
        }

        public void ClearCart(int userId)
        {
            lock (_sync)
            {
                if (_userCarts.ContainsKey(userId))
                {
                    _userCarts[userId].Clear();
                }
            }
        }

        public bool CartExists(int userId)
        {
            lock (_sync)
            {
                return _userCarts.ContainsKey(userId) && _userCarts[userId].Any();
            }
        }
    }
}
