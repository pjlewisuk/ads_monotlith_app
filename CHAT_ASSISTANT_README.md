# AI Copilot - Retail Chat Assistant

## Overview
This implementation adds an AI-powered shopping assistant to the Retail Monolith application using Azure OpenAI. The chat assistant can recommend products, answer customer questions, and help with shopping decisions.

## Features Implemented

### 1. Azure OpenAI Integration
- **Service Layer**: `Services/ChatService.cs` and `Services/IChatService.cs`
- **SDK**: Azure.AI.OpenAI v2.1.0
- **Pattern**: RAG (Retrieval Augmented Generation) with product catalog context

### 2. Chat Service Capabilities
- **Conversation Management**: Maintains per-user conversation history
- **Product Recommendations**: AI-powered product suggestions based on queries
- **Product Context**: Includes all active products in system prompt
- **SKU Extraction**: Automatically extracts product SKUs from AI responses using pattern `[PRODUCT:SKU-####]`

### 3. User Interface
- **Chat Page**: `/Pages/Chat/Index.cshtml` and `Index.cshtml.cs`
- **Features**:
  - Real-time conversation display
  - Product cards with "Add to Cart" buttons
  - Auto-scroll to latest message
  - Clear conversation option
  - Session-based conversation persistence (TempData)

### 4. Navigation
- Added "Shopping Assistant" link to main navigation
- Bootstrap Icons for chat icon

## Configuration Required

Users need to configure Azure OpenAI settings in `appsettings.json` or User Secrets:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://your-resource.openai.azure.com/",
    "ApiKey": "<your-api-key>",
    "DeploymentName": "gpt-4",
    "MaxTokens": "800",
    "Temperature": "0.7"
  }
}
```

### Setup Steps:
1. Create Azure OpenAI Service resource in Azure Portal
2. Deploy a GPT-4 or GPT-3.5-turbo model
3. Update configuration with:
   - Endpoint URL
   - API Key
   - Deployment name

## Code Structure

### Services
- **IChatService**: Interface defining chat operations
- **ChatService**: Implementation with Azure OpenAI integration
  - `GetChatResponseAsync()`: Handles user messages and returns AI responses
  - `GetProductRecommendationsAsync()`: Returns product recommendations
  - `ClearConversationHistoryAsync()`: Clears user's conversation history

### Pages
- **Pages/Chat/Index.cshtml.cs**: Page model handling chat logic
  - User message submission
  - Conversation state management
  - Product addition to cart
  - Conversation clearing
- **Pages/Chat/Index.cshtml**: Chat UI
  - Message display (user and assistant)
  - Product cards for recommendations
  - Input form for new messages

### Models
- **ChatResponse**: Record type containing AI message and suggested products

## Technical Highlights

1. **Latest Azure OpenAI SDK**: Uses v2.1.0 with new `AzureOpenAIClient` API
2. **Memory Management**: In-memory conversation history per user
3. **RAG Implementation**: Product catalog embedded in system prompt
4. **Product Extraction**: Regex-based SKU extraction from AI responses
5. **Bootstrap 5**: Modern, responsive UI design
6. **Bootstrap Icons**: Icon support for better UX

## Testing

To test the implementation:

1. Configure Azure OpenAI credentials (see Configuration section above)
2. Run the application: `dotnet run`
3. Navigate to "Shopping Assistant" in the navigation menu
4. Try queries like:
   - "Show me some electronics"
   - "I need a gift for my mom"
   - "What footwear do you have under £50?"
   - "Recommend beauty products"

## Implementation Status

✅ **Complete** - All required features implemented:
- Azure OpenAI integration
- Chat service with RAG pattern
- Conversation history management
- Product recommendations
- Chat UI with product cards
- Add to cart from chat
- Navigation integration
- Configuration structure

## Notes

- Conversation history is stored in-memory (per application instance)
- For production, consider:
  - Persistent conversation storage (database)
  - Rate limiting
  - Content filtering
  - Cost management (token limits)
  - Caching product context
  - Multi-instance session sharing (distributed cache)

## Dependencies Added
- Azure.AI.OpenAI (v2.1.0)
- Bootstrap Icons (CDN)
