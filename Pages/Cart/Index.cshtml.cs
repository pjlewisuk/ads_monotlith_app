using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RetailMonolith.Data;
using RetailMonolith.Services;
using Microsoft.ApplicationInsights;

namespace RetailMonolith.Pages.Cart
{
    public class IndexModel : PageModel
    {
       
      
        private readonly ICartService _cartService;
        private readonly ILogger<IndexModel> _logger;
        private readonly TelemetryClient? _telemetryClient;
       
        public IndexModel(ICartService cartService, ILogger<IndexModel> logger, TelemetryClient? telemetryClient = null)
        {
            _cartService = cartService;
            _logger = logger;
            _telemetryClient = telemetryClient;
        }



        //map the cart state in memory without mapping directly to the database
        // Each line represents an item in the cart with its name, quantity, and price
        //is this a good way to represent a cart in memory?
        public List<(string Name, int Quantity, decimal Price)> Lines { get; set; } = new(); 

        public decimal Total => Lines.Sum(line => line.Price * line.Quantity);


        public async Task OnGetAsync()
        {
            var cart = await _cartService.GetCartWithLinesAsync("guest");
            Lines = cart.Lines
                .Select(line => (line.Name, line.Quantity, line.UnitPrice))
                .ToList();
            
            _logger.LogInformation("Cart viewed with {ItemCount} items, total value {Total:C}", Lines.Count, Total);
            
            _telemetryClient?.TrackEvent("CartViewed", new Dictionary<string, string>
            {
                { "ItemCount", Lines.Count.ToString() },
                { "Total", Total.ToString("F2") }
            });
        }


    }
}
