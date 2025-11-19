using Microsoft.EntityFrameworkCore;
using RetailMonolith.Data;
using RetailMonolith.Models;
using System.Diagnostics;
using Microsoft.ApplicationInsights;

namespace RetailMonolith.Services
{
    public class CartService : ICartService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<CartService> _logger;
        private readonly TelemetryClient? _telemetryClient;
        private static readonly ActivitySource ActivitySource = new("RetailMonolith.CartService");
        
        public CartService(AppDbContext db, ILogger<CartService> logger, TelemetryClient? telemetryClient = null)
        {
            _db = db;
            _logger = logger;
            _telemetryClient = telemetryClient;
        }

        public async Task AddToCartAsync(string customerId, int productId, int quantity = 1, CancellationToken ct = default)
        {
            using var activity = ActivitySource.StartActivity("AddToCart");
            activity?.SetTag("customer.id", customerId);
            activity?.SetTag("product.id", productId);
            activity?.SetTag("quantity", quantity);
            
            try
            {
                _logger.LogInformation("Adding product {ProductId} to cart for customer {CustomerId}", productId, customerId);
                
                //get or create cart
                var cart = await GetOrCreateCartAsync(customerId, ct);

                //get product
                var product = await _db.Products.FindAsync(new object[] { productId }, ct);
                if (product is null)
                {
                    _logger.LogWarning("Invalid product ID {ProductId} for customer {CustomerId}", productId, customerId);
                    throw new InvalidOperationException("Invalid product ID");
                }

                activity?.SetTag("product.sku", product.Sku);
                activity?.SetTag("product.name", product.Name);
                activity?.SetTag("product.price", product.Price);

                //check if product already exists in cart
                var existing = cart.Lines.FirstOrDefault(line => line.Sku == product.Sku);

                //if exists, update quantity otherwise add new line
                if (existing is not null)
                {
                    existing.Quantity += quantity;
                    _logger.LogInformation("Updated quantity for {Sku} in cart, new quantity: {Quantity}", product.Sku, existing.Quantity);
                }
                else
                {
                    var line = new CartLine
                    {
                        CartId = cart.Id,
                        Sku = product.Sku,
                        Name = product.Name,
                        UnitPrice = product.Price,
                        Quantity = quantity
                    };
                    cart.Lines.Add(line);
                    _logger.LogInformation("Added new item {Sku} to cart", product.Sku);
                }

                await _db.SaveChangesAsync(ct);

                // Track custom event
                _telemetryClient?.TrackEvent("AddedToCart", new Dictionary<string, string>
                {
                    { "CustomerId", customerId },
                    { "ProductId", productId.ToString() },
                    { "ProductSku", product.Sku },
                    { "ProductName", product.Name },
                    { "Quantity", quantity.ToString() },
                    { "Price", product.Price.ToString("F2") }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add product {ProductId} to cart for customer {CustomerId}", productId, customerId);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
        }

        public async Task ClearCartAsync(string customerId, CancellationToken ct = default)
        {
            using var activity = ActivitySource.StartActivity("ClearCart");
            activity?.SetTag("customer.id", customerId);
            
            var cart = await _db.Carts
                .Include(c => c.Lines)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId, ct);
            if (cart is null)
            {
                _logger.LogInformation("No cart to clear for customer {CustomerId}", customerId);
                return;
            }

            _db.Carts.Remove(cart);
            await _db.SaveChangesAsync(ct);
            
            _logger.LogInformation("Cleared cart for customer {CustomerId}", customerId);
        }

        public async Task<Cart> GetCartWithLinesAsync(string customerId, CancellationToken ct = default)
        {
            //return cart if found otherwise return a new cart instance
            return await _db.Carts
                .Include(c => c.Lines)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId, ct) ?? new Cart { CustomerId = customerId };
        }

        public async Task<Cart> GetOrCreateCartAsync(string customerId, CancellationToken ct = default)
        {
            //get cart
            var cart = await _db.Carts
                .Include(c => c.Lines)
                .OrderBy(c => c.Id)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId, ct);

            //create cart if not found
            if (cart is null)
            {
                cart = new Cart { CustomerId = customerId };
                _db.Carts.Add(cart);
                await _db.SaveChangesAsync(ct);
            }
            //cart won't be null as we are creating a new instance of a cart if it is null
            return cart;
        }
    }
}
