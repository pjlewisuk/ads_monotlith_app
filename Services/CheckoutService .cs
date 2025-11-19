using Microsoft.EntityFrameworkCore;
using RetailMonolith.Data;
using RetailMonolith.Models;
using System.Diagnostics;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

namespace RetailMonolith.Services
{
    public class CheckoutService : ICheckoutService
    {
        private readonly AppDbContext _db;
        private readonly IPaymentGateway _payments;
        private readonly ILogger<CheckoutService> _logger;
        private readonly TelemetryClient? _telemetryClient;
        private static readonly ActivitySource ActivitySource = new("RetailMonolith.CheckoutService");

        public CheckoutService(AppDbContext db, IPaymentGateway payments, ILogger<CheckoutService> logger, TelemetryClient? telemetryClient = null)
        {
            _db = db;
            _payments = payments;
            _logger = logger;
            _telemetryClient = telemetryClient;
        }
        public async Task<Order> CheckoutAsync(string customerId, string paymentToken, CancellationToken ct = default)
        {
            using var activity = ActivitySource.StartActivity("Checkout");
            activity?.SetTag("customer.id", customerId);
            
            try
            {
                _logger.LogInformation("Starting checkout for customer {CustomerId}", customerId);
                
                // 1) pull cart
                var cart = await _db.Carts
                    .Include(c => c.Lines)
                    .FirstOrDefaultAsync(c => c.CustomerId == customerId, ct)
                    ?? throw new InvalidOperationException("Cart not found");

                var total = cart.Lines.Sum(l => l.UnitPrice * l.Quantity);
                activity?.SetTag("order.total", total);
                activity?.SetTag("order.items_count", cart.Lines.Count);

                // Track checkout initiated
                _telemetryClient?.TrackEvent("CheckoutInitiated", new Dictionary<string, string>
                {
                    { "CustomerId", customerId },
                    { "ItemCount", cart.Lines.Count.ToString() },
                    { "Total", total.ToString("F2") }
                });

                // 2) reserve/decrement stock (optimistic)
                using (var inventoryActivity = ActivitySource.StartActivity("ReserveInventory"))
                {
                    foreach (var line in cart.Lines)
                    {
                        var inv = await _db.Inventory.SingleAsync(i => i.Sku == line.Sku, ct);
                        if (inv.Quantity < line.Quantity)
                        {
                            _logger.LogWarning("Out of stock for SKU {Sku}. Required: {Required}, Available: {Available}", 
                                line.Sku, line.Quantity, inv.Quantity);
                            throw new InvalidOperationException($"Out of stock: {line.Sku}");
                        }
                        inv.Quantity -= line.Quantity;
                    }
                }

                // 3) charge
                using (var paymentActivity = ActivitySource.StartActivity("ProcessPayment"))
                {
                    paymentActivity?.SetTag("payment.amount", total);
                    paymentActivity?.SetTag("payment.currency", "GBP");
                    
                    var pay = await _payments.ChargeAsync(new(total, "GBP", paymentToken), ct);
                    var status = pay.Succeeded ? "Paid" : "Failed";
                    
                    paymentActivity?.SetTag("payment.status", status);
                    
                    if (pay.Succeeded)
                    {
                        _telemetryClient?.GetMetric("PaymentSuccess").TrackValue(1);
                        _logger.LogInformation("Payment succeeded for customer {CustomerId}, transaction {ProviderRef}", 
                            customerId, pay.ProviderRef);
                    }
                    else
                    {
                        _telemetryClient?.GetMetric("PaymentFailure").TrackValue(1);
                        _logger.LogWarning("Payment failed for customer {CustomerId}", customerId);
                    }

                    // 4) create order
                    var order = new Order { CustomerId = customerId, Status = status, Total = total };
                    order.Lines = cart.Lines.Select(l => new OrderLine
                    {
                        Sku = l.Sku,
                        Name = l.Name,
                        UnitPrice = l.UnitPrice,
                        Quantity = l.Quantity
                    }).ToList();

                    _db.Orders.Add(order);

                    // 5) clear cart
                    _db.CartLines.RemoveRange(cart.Lines);
                    await _db.SaveChangesAsync(ct);

                    // Track order completed
                    if (pay.Succeeded)
                    {
                        _telemetryClient?.TrackEvent("OrderCompleted", new Dictionary<string, string>
                        {
                            { "CustomerId", customerId },
                            { "OrderId", order.Id.ToString() },
                            { "OrderTotal", order.Total.ToString("F2") }
                        });
                        
                        _telemetryClient?.GetMetric("OrderValue").TrackValue(order.Total);
                        
                        _logger.LogInformation("Order {OrderId} completed successfully for customer {CustomerId} with total {Total:C}", 
                            order.Id, customerId, order.Total);
                    }

                    // (future) publish events here: OrderCreated / PaymentProcessed / InventoryReserved
                    return order;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Checkout failed for customer {CustomerId}", customerId);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                
                _telemetryClient?.TrackException(ex, new Dictionary<string, string>
                {
                    { "CustomerId", customerId },
                    { "Operation", "Checkout" }
                });
                
                throw;
            }
        }
    }
}
