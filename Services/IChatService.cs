using RetailMonolith.Models;

namespace RetailMonolith.Services
{
    public interface IChatService
    {
        Task<ChatResponse> GetChatResponseAsync(string userMessage, string userId, CancellationToken ct = default);
        Task<List<Product>> GetProductRecommendationsAsync(string query, CancellationToken ct = default);
        Task ClearConversationHistoryAsync(string userId, CancellationToken ct = default);
    }

    public record ChatResponse(string Message, List<Product>? SuggestedProducts = null);
}
