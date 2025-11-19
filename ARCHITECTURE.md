# Chat Assistant Architecture

## Component Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                        User Interface Layer                      │
│                                                                   │
│  ┌──────────────────┐      ┌─────────────────────────────────┐  │
│  │  Navigation Bar  │      │   Pages/Chat/Index.cshtml       │  │
│  │  - Shopping      │      │   - Chat container              │  │
│  │    Assistant     │──────│   - Message display             │  │
│  │    Link          │      │   - Product cards               │  │
│  └──────────────────┘      │   - Input form                  │  │
│                             │   - Clear button                │  │
│                             └─────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
                                       │
                                       │ User Input
                                       ▼
┌─────────────────────────────────────────────────────────────────┐
│                      Application Layer                           │
│                                                                   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  Pages/Chat/Index.cshtml.cs (Page Model)                 │   │
│  │  - OnPostAsync: Handle user messages                     │   │
│  │  - OnPostAddToCartAsync: Add products to cart            │   │
│  │  - OnPostClearConversationAsync: Clear history           │   │
│  │  - Conversation state management (TempData)              │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
                                       │
                                       │ Calls Service
                                       ▼
┌─────────────────────────────────────────────────────────────────┐
│                      Service Layer                               │
│                                                                   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  IChatService Interface                                  │   │
│  │  - GetChatResponseAsync                                  │   │
│  │  - GetProductRecommendationsAsync                        │   │
│  │  - ClearConversationHistoryAsync                         │   │
│  └──────────────────────────────────────────────────────────┘   │
│                             │                                    │
│                             │ Implemented by                     │
│                             ▼                                    │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  ChatService Implementation                              │   │
│  │  - AzureOpenAIClient integration                         │   │
│  │  - Conversation history per user (in-memory)             │   │
│  │  - RAG pattern with product catalog                      │   │
│  │  - SKU extraction via regex                              │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
                │                              │
                │ Queries DB                   │ Calls Azure OpenAI
                ▼                              ▼
┌────────────────────────┐      ┌──────────────────────────────┐
│   Data Layer           │      │   External Service           │
│                        │      │                              │
│  AppDbContext          │      │   Azure OpenAI API           │
│  - Products            │      │   - GPT-4 / GPT-3.5-turbo   │
│  - Inventory           │      │   - Chat Completions         │
│  - Carts               │      │                              │
└────────────────────────┘      └──────────────────────────────┘
```

## Data Flow

### User Sends Message:

1. **User**: Types message in chat input
2. **Page Model**: Receives POST request
3. **Chat Service**: 
   - Retrieves conversation history for user
   - If new conversation: Load product catalog from DB
   - Build system prompt with product context (RAG)
   - Add user message to history
   - Call Azure OpenAI API
4. **Azure OpenAI**: Generates response based on context
5. **Chat Service**: 
   - Extracts product SKUs from response
   - Queries DB for suggested products
   - Returns ChatResponse with message + products
6. **Page Model**: 
   - Stores conversation in TempData
   - Redirects to display updated chat
7. **UI**: Displays message and product cards

### Add to Cart from Chat:

1. **User**: Clicks "Add to Cart" on product card
2. **Page Model**: Receives POST with productId
3. **Cart Service**: Adds product to user's cart
4. **UI**: Refreshes page maintaining chat state

## Key Features

### RAG Implementation
- All active products loaded on first message
- Product catalog embedded in system prompt
- Provides context: SKU, Name, Category, Price, Description
- AI can reference specific products in responses

### Conversation Management
- Dictionary<userId, List<ChatMessage>> for history
- Scoped to application instance (in-memory)
- Supports multiple concurrent users
- Can be cleared per user

### Product Extraction
- Pattern: `[PRODUCT:SKU-####]`
- Regex matching on AI responses
- Automatic product card generation
- Direct cart integration

## Configuration

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

## Dependencies

- **Azure.AI.OpenAI**: v2.1.0
- **Bootstrap**: 5.3.2 (existing)
- **Bootstrap Icons**: 1.11.3 (added)
- **Entity Framework Core**: 9.0.9 (existing)

## Files Changed/Added

### New Files:
- `Services/IChatService.cs` - Interface definition
- `Services/ChatService.cs` - Azure OpenAI integration
- `Pages/Chat/Index.cshtml` - Chat UI
- `Pages/Chat/Index.cshtml.cs` - Page model
- `CHAT_ASSISTANT_README.md` - Documentation
- `ARCHITECTURE.md` - This file

### Modified Files:
- `Program.cs` - Service registration
- `appsettings.json` - Configuration structure
- `Pages/Shared/_Layout.cshtml` - Navigation + Bootstrap Icons
- `RetailMonolith.csproj` - NuGet package reference
