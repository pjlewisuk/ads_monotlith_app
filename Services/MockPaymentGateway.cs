using System.Diagnostics;
using Microsoft.ApplicationInsights;

namespace RetailMonolith.Services
{
    public class MockPaymentGateway : IPaymentGateway
    {
        private static readonly ActivitySource ActivitySource = new("RetailMonolith.PaymentGateway");
        private readonly ILogger<MockPaymentGateway> _logger;
        private readonly TelemetryClient? _telemetryClient;
        
        public MockPaymentGateway(ILogger<MockPaymentGateway> logger, TelemetryClient? telemetryClient = null)
        {
            _logger = logger;
            _telemetryClient = telemetryClient;
        }
        
        public Task<PaymentResult> ChargeAsync(PaymentRequest req, CancellationToken ct = default)
        {
            using var activity = ActivitySource.StartActivity("ProcessPayment");
            activity?.SetTag("payment.amount", req.Amount);
            activity?.SetTag("payment.currency", req.Currency);
            
            _logger.LogInformation("Processing payment for {Amount} {Currency}", req.Amount, req.Currency);
            
            // trivial success for hack; add a random fail to demo error path if you like
            var transactionId = $"MOCK-{Guid.NewGuid():N}";
            var result = new PaymentResult(true, transactionId, null);
            
            _telemetryClient?.TrackEvent("PaymentProcessed", new Dictionary<string, string>
            {
                { "Amount", req.Amount.ToString("F2") },
                { "Currency", req.Currency },
                { "TransactionId", transactionId },
                { "Success", "true" }
            });
            
            _logger.LogInformation("Payment processed successfully. Transaction ID: {TransactionId}", transactionId);
            
            return Task.FromResult(result);
        }
    }
}
