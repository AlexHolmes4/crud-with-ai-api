# AI-Powered Product CRUD API

A conversational AI-powered REST API for product management using Claude (Anthropic) with tool-based function calling. Users interact with the system through natural language, and Claude translates requests into structured CRUD operations. Front end application (https://github.com/alexholmes4/crud-with-ai-ui)

## Overview

This is a portfolio demonstration project showcasing:
- Clean Architecture implementation
- AI tool integration patterns
- Test-driven development
- Modern .NET practices
- Production-ready API design

This API demonstrates an **agentic AI pattern** where:
- Users send natural language requests to manage products
- Claude AI analyzes intent and executes appropriate operations
- Business logic remains pure and AI-agnostic
- Results are returned conversationally with structured data

## Technology Stack

- **.NET 10.0** - Framework
- **ASP.NET Core** - Web API
- **Anthropic SDK 5.8.0** - Claude AI integration
- **Claude 4.5 Haiku** - AI model
- **Entity Framework Core** - ORM
- **In-Memory Database** - Data storage
- **Mapster 9.0** - Object mapping
- **Swashbuckle 7.2.0** - OpenAPI/Swagger

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                   User Request                              │
│         "Create a wireless mouse for $29.99"                │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│              API Layer (ASP.NET Core)                       │
│              ConversationsController                        │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│              Application Layer                              │
│  ┌──────────────────────────────────────────────────┐       │
│  │ ConversationService (AI Orchestration)           │       │
│  │ • Manages conversation history                   │       │
│  │ • Calls Claude API with tools                    │       │
│  │ • Executes tool calls                            │       │
│  │ • Returns natural language responses             │       │
│  └──────────────────────────────────────────────────┘       │
│  ┌──────────────────────────────────────────────────┐       │
│  │ AnthropicToolService (Tool Definitions)          │       │
│  │ • Defines available tools (find, create, etc.)   │       │
│  │ • Provides JSON schemas for parameters           │       │
│  └──────────────────────────────────────────────────┘       │
│  ┌──────────────────────────────────────────────────┐       │
│  │ ProductService (Business Logic)                  │       │
│  │ • CRUD operations                                │       │
│  │ • SKU uniqueness validation                      │       │
│  │ • Multiple name match handling                   │       │
│  └──────────────────────────────────────────────────┘       │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│              Domain & Infrastructure                        │
│  • Product Entity                                           │
│  • ProductRepository (In-Memory Database)                   │
└─────────────────────────────────────────────────────────────┘
```


## Example Conversations

### Create Product

**User:** "Create a wireless mouse for $29.99 with SKU WM-001"

**Assistant:** "I've successfully created the Wireless Mouse product for you!

**Product: Wireless Mouse**
- Description: Ergonomic wireless mouse
- Price: $29.99
- SKU: WM-001"

### Find Product

**User:** "Find the wireless mouse"

**Assistant:** "Here's the product I found:

**Product: Wireless Mouse**
- Description: Ergonomic wireless mouse
- Price: $29.99
- SKU: WM-001"

### Update Product

**User:** "Update the wireless mouse price to $24.99"

**Assistant:** "I've updated the Wireless Mouse product:

**Product: Wireless Mouse**
- Description: Ergonomic wireless mouse
- Price: $24.99 (updated from $29.99)
- SKU: WM-001"

### Error Handling

**User:** "Create a product with SKU WM-001" (already exists)

**Assistant:** "I wasn't able to create that product because the SKU WM-001 is already in use. Could you provide a different SKU?"

## How It Works: Create Product Flow

### 1. User Sends Natural Language Request

```http
POST /api/v1/conversations/messages
Content-Type: application/json

{
  "prompt": "Create a wireless mouse for $29.99 with SKU WM-001",
  "conversationId": "optional-conversation-id"
}
```

### 2. Conversation Initialization

- **ConversationService** retrieves or creates a conversation
- Adds user message to conversation history
- Gets tool definitions from **AnthropicToolService**
- Builds request with system prompt and tools

### 3. First Claude API Call

Claude receives:
- User's natural language message
- 5 available tool definitions (find, list, create, update, delete)
- System prompt with guidelines

Claude analyzes and returns:
```json
{
  "role": "assistant",
  "content": [{
    "type": "tool_use",
    "name": "create_product",
    "input": {
      "name": "Wireless Mouse",
      "description": "Ergonomic wireless mouse",
      "price": 29.99,
      "sku": "WM-001"
    }
  }]
}
```

### 4. Tool Execution

**ConversationService** routes the tool call:
```csharp
toolName switch
{
    "create_product" => await ExecuteCreateProduct(...),
    // ... other tools
}
```

**ExecuteCreateProduct** method:
1. Extracts parameters from Claude's JSON
2. Creates `ProductRequest` DTO
3. Validates required fields
4. Calls `ProductService.CreateProductAsync()`

### 5. Business Logic Execution

**ProductService** performs:
1. **SKU Validation**: Checks for duplicate SKU
2. **DTO → Entity Mapping**: Uses Mapster
3. **Database Persistence**: Saves via repository
4. **Entity → DTO Mapping**: Returns `ProductResponse`

### 6. Tool Result Injection

Result is added back to conversation:
```json
{
  "role": "user",
  "content": [{
    "type": "tool_result",
    "tool_use_id": "toolu_123",
    "content": "{\"name\":\"Wireless Mouse\",\"sku\":\"WM-001\",...}"
  }]
}
```

### 7. Second Claude API Call (Agentic Loop)

Claude sees the tool result and generates natural language response:
```
"I've successfully created the Wireless Mouse product for you!

**Product: Wireless Mouse**
- Description: Ergonomic wireless mouse
- Price: $29.99
- SKU: WM-001

The product is now available in the system."
```

### 8. Response to User

```json
{
  "conversationId": "abc-123",
  "processedAction": "create_product",
  "affectedProduct": {
    "name": "Wireless Mouse",
    "description": "Ergonomic wireless mouse",
    "price": 29.99,
    "sku": "WM-001",
    "createdAt": "2025-12-31T10:30:00Z",
    "updatedAt": null
  },
  "messages": [
    {
      "role": "user",
      "content": "Create a wireless mouse for $29.99..."
    },
    {
      "role": "assistant",
      "content": "I've successfully created the Wireless Mouse..."
    }
  ]
}
```

### Available Tools

| Tool             | Description                 |
|------------------|-----------------------------|
| `find_product`   | Find a specific product     |
| `list_products`  | List all or search products |
| `create_product` | Create a new product        |
| `update_product` | Update existing product     |
| `delete_product` | Delete a product            |

### How Claude Uses Tools

1. **Intent Recognition**: Analyzes user message
2. **Tool Matching**: Compares intent to tool descriptions
3. **Parameter Extraction**: Pulls values from natural language
4. **Schema Validation**: Ensures parameters match JSON schema
5. **Returns Tool Call**: Structured JSON with tool name + parameters

## Future Enhancements

- [ ] Persistent database (SQL Server, PostgreSQL)
- [ ] Authentication & authorization
- [ ] Bulk operations support
- [ ] Product categories and tags
- [ ] Image upload and management
- [ ] Inventory tracking
- [ ] Price history
- [ ] Advanced search with filters
- [ ] Export functionality (CSV, Excel)
- [ ] Audit logging

## License

MIT License

## Acknowledgments
- Part of this README was generated with AI.
- [Anthropic SDK](https://github.com/tghamm/Anthropic.SDK) - C# SDK for Anthropic's Claude API
- Claude 4.5 Haiku - The AI model powering conversational interactions
