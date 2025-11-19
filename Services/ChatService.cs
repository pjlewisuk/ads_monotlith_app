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
        private readonly AzureOpenAIClient _openAIClient;
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;
        private readonly Dictionary<string, List<ChatMessage>> _conversationHistory = new();

        public ChatService(IConfiguration config, AppDbContext db)
        {
            _config = config;
            _db = db;
            
            var endpoint = new Uri(config["AzureOpenAI:Endpoint"] ?? "https://placeholder.openai.azure.com/");
            var apiKey = new AzureKeyCredential(config["AzureOpenAI:ApiKey"] ?? "placeholder-key");
            _openAIClient = new AzureOpenAIClient(endpoint, apiKey);
        }

        public async Task<ChatResponse> GetChatResponseAsync(string userMessage, string userId, CancellationToken ct = default)
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

        public async Task<List<Product>> GetProductRecommendationsAsync(string query, CancellationToken ct = default)
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
