using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RetailMonolith.Data;
using RetailMonolith.Models;
using RetailMonolith.Services;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.ApplicationInsights;

namespace RetailMonolith.Pages.Products
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ICartService _cartService;
        private readonly ILogger<IndexModel> _logger;
        private readonly TelemetryClient? _telemetryClient;
        private static readonly ActivitySource ActivitySource = new("RetailMonolith.Pages.Products");
        
        public IndexModel(AppDbContext db, ICartService cartService, ILogger<IndexModel> logger, TelemetryClient? telemetryClient = null)
        {
            _db = db;
            _cartService = cartService;
            _logger = logger;
            _telemetryClient = telemetryClient;
        }

        public IList<Product> Products { get; set; } = new List<Product>();

        // Category ? Image mapping
        public static readonly Dictionary<string, string> CategoryImages = new()
        {
            ["Beauty"] = "https://images.unsplash.com/photo-1596462502278-27bfdc403348?auto=format&fit=crop&w=800&q=80",
            ["Apparel"] = "https://images.unsplash.com/photo-1489987707025-afc232f7ea0f?auto=format&fit=crop&w=800&q=80",
            ["Footwear"] = "https://images.unsplash.com/photo-1603808033192-082d6919d3e1?auto=format&fit=crop&w=800&q=80",
            ["Home"] = "https://images.unsplash.com/photo-1583847268964-b28dc8f51f92?auto=format&fit=crop&w=800&q=80",
            ["Accessories"] = "https://images.unsplash.com/photo-1586878341523-7acb55eb8c12?auto=format&fit=crop&w=800&q=80",
            ["Electronics"] = "https://images.unsplash.com/photo-1498049794561-7780e7231661?auto=format&fit=crop&w=800&q=80"
        };

        // Helper method accessible from the Razor page
        public string GetImageForCategory(string category)
        {
            if (CategoryImages.TryGetValue(category ?? string.Empty, out var url))
                return url;

            // Fallback image if category missing
            return "https://images.unsplash.com/photo-1526170375885-4d8ecf77b99f?auto=format&fit=crop&w=800&q=80";
        }

        public async Task OnGetAsync()
        {
            Products = await _db.Products.Where(p => p.IsActive).ToListAsync();
            
            _logger.LogInformation("Products page viewed, showing {Count} products", Products.Count);
            
            _telemetryClient?.TrackEvent("ProductsViewed", new Dictionary<string, string>
            {
                { "ProductCount", Products.Count.ToString() }
            });
        }

        public async Task OnPostAsync(int productId)
        {
            using var activity = ActivitySource.StartActivity("AddProductToCart");
            activity?.SetTag("product.id", productId);
            
            // Add to cart logic will go here in the future
            var p = await _db.Products.FindAsync(productId);
            if (p is null)
            {
                _logger.LogWarning("Attempted to add non-existent product {ProductId} to cart", productId);
                return;
            }

            _logger.LogInformation("Adding product {ProductId} ({ProductName}) to cart", productId, p.Name);
            
            // Track product viewed
            _telemetryClient?.TrackEvent("ProductViewed", new Dictionary<string, string>
            {
                { "ProductId", productId.ToString() },
                { "ProductSku", p.Sku },
                { "ProductName", p.Name },
                { "Category", p.Category ?? "Unknown" }
            });

            var cart = await _db.Carts
                .Include(c => c.Lines)
                .FirstOrDefaultAsync(c => c.CustomerId == "guest")
                ?? new Models.Cart { CustomerId = "guest" };

            //if(cart.Id == 0)
            //{
            //    _db.Carts.Add(cart);
            //};

            if (cart is null)
            {
                cart = new Models.Cart { CustomerId = "guest" };
                _db.Carts.Add(cart);
                await _db.SaveChangesAsync();
            }

            cart.Lines.Add(new CartLine
            {
                Sku = p.Sku,
                Name = p.Name,
                UnitPrice = p.Price,
                Quantity = 1
            });
            await _cartService.AddToCartAsync("guest", productId);
            Response.Redirect("/Cart");
        }
    }
}
