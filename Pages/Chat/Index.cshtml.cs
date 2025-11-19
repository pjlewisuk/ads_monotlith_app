using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RetailMonolith.Models;
using RetailMonolith.Services;

namespace RetailMonolith.Pages.Chat
{
    public class IndexModel : PageModel
    {
        private readonly IChatService _chatService;
        private readonly ICartService _cartService;

        public IndexModel(IChatService chatService, ICartService cartService)
        {
            _chatService = chatService;
            _cartService = cartService;
        }

        [BindProperty]
        public string UserMessage { get; set; } = string.Empty;

        public List<(string Role, string Message, List<Product>? Products)> Conversation { get; set; } = new();

        public void OnGet()
        {
            // Load conversation from TempData if available
            var conversation = TempData["Conversation"] as string;
            if (!string.IsNullOrEmpty(conversation))
            {
                Conversation = System.Text.Json.JsonSerializer.Deserialize<List<(string, string, List<Product>?)>>(conversation) ?? new();
                TempData.Keep("Conversation");
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(UserMessage))
                return Page();

            try
            {
                var userId = User.Identity?.Name ?? "guest"; // Use authenticated user if available
                var response = await _chatService.GetChatResponseAsync(UserMessage, userId);

                // Store conversation in TempData for display
                var conversation = new List<(string, string, List<Product>?)>();
                
                var existingConversation = TempData["Conversation"] as string;
                if (!string.IsNullOrEmpty(existingConversation))
                {
                    conversation = System.Text.Json.JsonSerializer.Deserialize<List<(string, string, List<Product>?)>>(existingConversation) ?? new();
                }
                
                conversation.Add(("User", UserMessage, null));
                conversation.Add(("Assistant", response.Message, response.SuggestedProducts));
                
                TempData["Conversation"] = System.Text.Json.JsonSerializer.Serialize(conversation);

                return RedirectToPage();
            }
            catch (Exception ex)
            {
                // Log the error and show a friendly message
                TempData["ErrorMessage"] = $"An error occurred while processing your message. Please try again. Error: {ex.Message}";
                return RedirectToPage();
            }
        }

        public async Task<IActionResult> OnPostAddToCartAsync(int productId)
        {
            var userId = User.Identity?.Name ?? "guest";
            await _cartService.AddToCartAsync(userId, productId);
            TempData.Keep("Conversation");
            return RedirectToPage();
        }

        public IActionResult OnPostClearConversationAsync()
        {
            var userId = User.Identity?.Name ?? "guest";
            _chatService.ClearConversationHistoryAsync(userId);
            TempData.Remove("Conversation");
            return RedirectToPage();
        }
    }
}
