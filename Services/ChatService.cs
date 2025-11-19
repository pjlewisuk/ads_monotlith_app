using Azure.AI.OpenAI;
using Azure;
using Microsoft.EntityFrameworkCore;
using RetailMonolith.Data;
using RetailMonolith.Models;
using OpenAI.Chat;

namespace RetailMonolith.Services
{
    public class ChatService : IChatService
    {
        private readonly AzureOpenAIClient? _openAIClient;
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;
        private readonly Dictionary<string, List<ChatMessage>> _conversationHistory = new();
        private readonly bool _isConfigured;

        public ChatService(IConfiguration config, AppDbContext db)
        {
            _config = config;
            _db = db;
            
            var endpoint = config["AzureOpenAI:Endpoint"];
            var apiKey = config["AzureOpenAI:ApiKey"];
            
            // Check if Azure OpenAI is properly configured
            _isConfigured = !string.IsNullOrEmpty(endpoint) && 
                           !string.IsNullOrEmpty(apiKey) &&
                           !endpoint.Contains("your-resource") &&
                           !apiKey.Contains("your-api-key") &&
                           apiKey != "placeholder-key";
            
            if (_isConfigured)
            {
                _openAIClient = new AzureOpenAIClient(new Uri(endpoint!), new AzureKeyCredential(apiKey!));
            }
        }

        public async Task<ChatResponse> GetChatResponseAsync(string userMessage, string userId, CancellationToken ct = default)
        {
            // Check if Azure OpenAI is configured
            if (!_isConfigured || _openAIClient == null)
            {
                return new ChatResponse(
                    "The chat assistant is not currently available. Please configure Azure OpenAI credentials in appsettings.json:\n\n" +
                    "1. Set AzureOpenAI:Endpoint to your Azure OpenAI endpoint\n" +
                    "2. Set AzureOpenAI:ApiKey to your API key\n" +
                    "3. Set AzureOpenAI:DeploymentName to your deployed model name\n\n" +
                    "See CHAT_ASSISTANT_README.md for detailed instructions.",
                    null);
            }

            try
            {
                // Initialize conversation history if needed
                if (!_conversationHistory.ContainsKey(userId))
                {
                    _conversationHistory[userId] = new List<ChatMessage>();
                    
                    // Add system message with product context
                    var products = await _db.Products.Where(p => p.IsActive).ToListAsync(ct);
                    var productContext = BuildProductContext(products);
                    
                    _conversationHistory[userId].Add(new SystemChatMessage(
                        $@"You are a helpful retail shopping assistant. You help customers find products and make recommendations.
                        
                        Available products:
                        {productContext}
                        
                        When recommending products:
                        - Provide specific product names and prices
                        - Explain why they match the customer's needs
                        - Suggest complementary items when appropriate
                        - Be friendly and helpful
                        - If a product is not available, suggest alternatives
                        
                        Format product recommendations as: [PRODUCT:SKU] so they can be highlighted."));
                }

                // Add user message
                _conversationHistory[userId].Add(new UserChatMessage(userMessage));

                // Get completion
                var deploymentName = _config["AzureOpenAI:DeploymentName"] ?? "gpt-4";
                var chatClient = _openAIClient.GetChatClient(deploymentName);
                
                var chatOptions = new ChatCompletionOptions
                {
                    MaxOutputTokenCount = int.Parse(_config["AzureOpenAI:MaxTokens"] ?? "800"),
                    Temperature = float.Parse(_config["AzureOpenAI:Temperature"] ?? "0.7")
                };

                var response = await chatClient.CompleteChatAsync(_conversationHistory[userId], chatOptions, ct);
                var assistantMessage = response.Value.Content[0].Text;

                // Add assistant response to history
                _conversationHistory[userId].Add(new AssistantChatMessage(assistantMessage));

                // Extract product SKUs from response
                var suggestedProducts = await ExtractSuggestedProductsAsync(assistantMessage, ct);

                return new ChatResponse(assistantMessage, suggestedProducts);
            }
            catch (Exception ex)
            {
                // Log error and return friendly message
                return new ChatResponse(
                    $"I'm sorry, I encountered an error while processing your request. Please try again later or contact support if the problem persists.\n\n" +
                    $"Error: {ex.Message}",
                    null);
            }
        }

        public async Task<List<Product>> GetProductRecommendationsAsync(string query, CancellationToken ct = default)
        {
            // Check if Azure OpenAI is configured
            if (!_isConfigured || _openAIClient == null)
            {
                return new List<Product>();
            }

            try
            {
                var products = await _db.Products
                    .Where(p => p.IsActive)
                    .ToListAsync(ct);

                var productContext = BuildProductContext(products);

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(
                        $@"You are a product recommendation expert. Based on the user's query, recommend 3-5 products from the catalog.
                        
                        Available products:
                        {productContext}
                        
                        Respond with only the SKU codes, separated by commas. No explanations."),
                    new UserChatMessage(query)
                };

                var deploymentName = _config["AzureOpenAI:DeploymentName"] ?? "gpt-4";
                var chatClient = _openAIClient.GetChatClient(deploymentName);

                var response = await chatClient.CompleteChatAsync(messages, cancellationToken: ct);
                var skuList = response.Value.Content[0].Text;

                var skus = skuList.Split(',')
                    .Select(s => s.Trim().Replace("[PRODUCT:", "").Replace("]", ""))
                    .ToList();

                return await _db.Products
                    .Where(p => skus.Contains(p.Sku))
                    .ToListAsync(ct);
            }
            catch
            {
                return new List<Product>();
            }
        }

        public Task ClearConversationHistoryAsync(string userId, CancellationToken ct = default)
        {
            _conversationHistory.Remove(userId);
            return Task.CompletedTask;
        }

        private string BuildProductContext(List<Product> products)
        {
            return string.Join("\n", products.Select(p => 
                $"SKU: {p.Sku}, Name: {p.Name}, Category: {p.Category}, Price: {p.Price:C} {p.Currency}, Description: {p.Description}"));
        }

        private async Task<List<Product>?> ExtractSuggestedProductsAsync(string message, CancellationToken ct)
        {
            // Extract SKUs mentioned in format [PRODUCT:SKU-####]
            var matches = System.Text.RegularExpressions.Regex.Matches(message, @"\[PRODUCT:(SKU-\d+)\]");
            
            if (matches.Count == 0) return null;

            var skus = matches.Select(m => m.Groups[1].Value).ToList();
            return await _db.Products.Where(p => skus.Contains(p.Sku)).ToListAsync(ct);
        }
    }
}
