using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RetailMonolith.Services;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.ApplicationInsights;

namespace RetailMonolith.Pages.Checkout
{
    public class IndexModel : PageModel
    {
        private readonly ICartService _cartService;
        private readonly ICheckoutService _checkoutService;
        private readonly ILogger<IndexModel> _logger;
        private readonly TelemetryClient? _telemetryClient;
        private static readonly ActivitySource ActivitySource = new("RetailMonolith.Pages.Checkout");
        
        public IndexModel(ICartService cartService, ICheckoutService checkoutService, ILogger<IndexModel> logger, TelemetryClient? telemetryClient = null)
        {
            _cartService = cartService;
            _checkoutService = checkoutService;
            _logger = logger;
            _telemetryClient = telemetryClient;
        }

        // For simplicity, using a hardcoded customer ID
        // In a real application, this would come from the authenticated user context
        // or session  
        public List<(string Name, int Qty, decimal Price)> Lines { get; set; } = new();

        public decimal Total => Lines.Sum(l => l.Price * l.Qty);

        [BindProperty]
        public string PaymentToken { get; set; } = "tok_test";

        public async Task OnGetAsync()
        {
            var cart = await _cartService.GetCartWithLinesAsync("guest");
            Lines = cart.Lines
                .Select(line => (line.Name, line.Quantity, line.UnitPrice))
                .ToList();
            
            _logger.LogInformation("Checkout page viewed with {ItemCount} items, total {Total:C}", Lines.Count, Total);
        }

        public async Task<IActionResult> OnPostAsync()
        {
           using var activity = ActivitySource.StartActivity("ProcessCheckout");
           
           if(!ModelState.IsValid)
           {
                await OnGetAsync();
                return Page();
            }

            try
            {
                _logger.LogInformation("Processing checkout for guest customer");
                
                //perform checkout using MockPaymentGateway
                var order = await _checkoutService.CheckoutAsync("guest", PaymentToken);

                _logger.LogInformation("Checkout successful, redirecting to order {OrderId}", order.Id);
                
                // redirect to order confirmation page
                return Redirect($"/Orders/Details?id={order.Id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Checkout failed for guest customer");
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
        }
    }
}
