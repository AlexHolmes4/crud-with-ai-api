using Anthropic.SDK;
using Anthropic.SDK.Common;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;
using CrudApi.Application.Dtos;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CrudApi.Application.Services;

public interface IConversationService
{
    Task<ChatPromptResponse> ProcessPromptAsync(
        ChatPromptRequest request,
        IProductService productService,
        CancellationToken cancellationToken = default);
}

public class ConversationService : IConversationService
{
    private readonly AnthropicClient _anthropicClient;
    private readonly IAnthropicToolService _toolService;

    // In-memory conversation storage (replace with database in production)
    private readonly Dictionary<string, List<Message>> _conversations = new();

    public ConversationService(AnthropicClient anthropicClient, IAnthropicToolService toolService)
    {
        _anthropicClient = anthropicClient;
        _toolService = toolService;
    }

    public async Task<ChatPromptResponse> ProcessPromptAsync(
        ChatPromptRequest request,
        IProductService productService,
        CancellationToken cancellationToken = default)
    {
        var conversationId = request.ConversationId ?? Guid.NewGuid().ToString();

        // Initialize conversation if new
        if (!_conversations.ContainsKey(conversationId))
        {
            _conversations[conversationId] = new();
        }

        var messages = _conversations[conversationId];

        // Add user prompt to conversation
        messages.Add(new Message(RoleType.User, request.Prompt));

        var response = new ChatPromptResponse
        {
            ConversationId = conversationId,
            Messages = new()
        };

        // Get tools for Claude
        var tools = _toolService.GetProductTools();

        // Create message parameters with system prompt
        var parameters = new MessageParameters
        {
            Messages = messages,
            MaxTokens = 2048,
            Model = AnthropicModels.Claude45Haiku,
            Tools = tools,
            ToolChoice = new ToolChoice { Type = ToolChoiceType.Auto },
            System = new List<SystemMessage>
            {
                new SystemMessage(GetSystemPrompt())
            }
        };

        // Call Claude
        var claudeResponse = await _anthropicClient.Messages.GetClaudeMessageAsync(parameters, cancellationToken);

        // Add assistant response to conversation
        messages.Add(claudeResponse.Message);

        // Process tool calls if any
        if (claudeResponse.ToolCalls.Any())
        {
            foreach (var toolCall in claudeResponse.ToolCalls)
            {
                var (toolResult, affectedProduct) = await ExecuteToolAsync(
                    toolCall.Name,
                    toolCall.Arguments,
                    productService,
                    cancellationToken);

                // Add tool result to conversation
                messages.Add(new Message
                {
                    Role = RoleType.User,
                    Content = new List<ContentBase>
                    {
                        new ToolResultContent
                        {
                            ToolUseId = toolCall.Id,
                            Content = new List<ContentBase>
                            {
                                new TextContent { Text = toolResult }
                            }
                        }
                    }
                });

                response.ProcessedAction = toolCall.Name;

                // Set the affected product if available
                if (affectedProduct != null)
                {
                    response.AffectedProduct = affectedProduct;
                }
            }

            // Get follow-up response from Claude after tool execution
            var followUpResponse = await _anthropicClient.Messages.GetClaudeMessageAsync(parameters, cancellationToken);
            messages.Add(followUpResponse.Message);
        }

        // Convert messages to DTOs
        response.Messages = messages
            .Where(m => m.Role == RoleType.User || m.Role == RoleType.Assistant)
            .Select(m => new ChatMessageDto
            {
                Role = m.Role.ToString().ToLower(),
                Content = m.ToString()
            })
            .ToList();

        return response;
    }

    private static string GetSystemPrompt()
    {
        return @"You are a helpful AI assistant for a product management system. Your role is to help users manage products through natural language conversation.

## Available Operations
- Find products by NAME or SKU (not by ID)
- List all products or search by name/description
- Create new products (required: Name, Description, Price, SKU - all required, SKU must be unique)
- Update existing products (by Name or SKU - all fields required)
- Delete products (by Name or SKU)

## Product Display Format
ALWAYS display products using this exact format (DO NOT show CreatedAt or UpdatedAt):

**Product: [Name]**
- Description: [Description]
- Price: $[Price]
- SKU: [SKU]

Example:
**Product: Wireless Mouse**
- Description: Ergonomic wireless mouse with 6 programmable buttons
- Price: $29.99
- SKU: WM-001

## Creating Products
When users want to create a product, inform them of the required fields:
""To create a product, I need:
- Name (required)
- Description (required)
- Price (required)
- SKU (required - must be unique)""

If any required field is missing, ask for it specifically. SKU must be unique across all products.

## Guidelines
1. ALWAYS use the exact display format shown above when presenting products
2. Users can ONLY find/update/delete products by Name or SKU, never by ID
3. When users ask to ""add"", ""create"", or ""make"" a product, use the create_product tool
4. When users ask to ""show"", ""find"", ""get"", or ""display"" a product, use find_product (by name/SKU) or list_products
5. When users ask to ""change"", ""update"", or ""modify"" a product, use update_product (by name/SKU)
6. When users ask to ""remove"", ""delete"", or ""get rid of"" a product, use delete_product (by name/SKU)
7. If a user's request is ambiguous or missing required fields, ask clarifying questions
8. Always confirm destructive actions (updates and deletes) by showing the product details first
9. Always confirm the provided information before creating or updating a product
11. When listing multiple products, use the same format for each one

## Important Rules
- NEVER expose or reference product IDs to users
- ALWAYS validate that Name, Description, and Price are provided for new products
- Be conversational but consistent in formatting
- If a product name or SKU doesn't exist, offer to list available products or create a new one
- ALWAYS confirm with the user before performing create, update, or delete actions, showing the product details

Remember: Consistency in presentation helps users quickly understand product information.";
    }

    private async Task<(string result, ProductResponse? affectedProduct)> ExecuteToolAsync(
        string toolName,
        JsonNode? toolInput,
        IProductService productService,
        CancellationToken cancellationToken)
    {
        try
        {
            return toolName switch
            {
                "find_product" => await ExecuteFindProduct(toolInput, productService, cancellationToken),
                "list_products" => await ExecuteListProducts(toolInput, productService, cancellationToken),
                "create_product" => await ExecuteCreateProduct(toolInput, productService, cancellationToken),
                "update_product" => await ExecuteUpdateProduct(toolInput, productService, cancellationToken),
                "delete_product" => await ExecuteDeleteProduct(toolInput, productService, cancellationToken),
                _ => ($"Unknown tool: {toolName}", null)
            };
        }
        catch (Exception ex)
        {
            return ($"Error executing {toolName}: {ex.Message}", null);
        }
    }

    private async Task<(string result, ProductResponse? affectedProduct)> ExecuteFindProduct(
        JsonNode? input,
        IProductService productService,
        CancellationToken cancellationToken)
    {
        if (input == null) return ("Product name or SKU is required", null);

        var name = input["name"]?.GetValue<string>();
        var sku = input["sku"]?.GetValue<string>();

        if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(sku))
            return ("Either product name or SKU must be provided", null);

        if (!string.IsNullOrEmpty(name))
        {
            // Check for multiple matches by name
            var matches = await productService.GetProductsByNameAsync(name, cancellationToken);

            if (matches.Count == 0)
                return ($"No products found with name '{name}'", null);

            if (matches.Count > 1)
            {
                var skuList = string.Join(", ", matches.Select(p => p.Sku));
                return ($"Multiple products found with name '{name}'. Please specify which one using its SKU. Available SKUs: {skuList}", null);
            }

            return (JsonSerializer.Serialize(matches[0]), matches[0]);
        }
        else if (!string.IsNullOrEmpty(sku))
        {
            var product = await productService.GetProductBySkuAsync(sku, cancellationToken);

            if (product == null)
                return ($"No product found with SKU '{sku}'", null);

            return (JsonSerializer.Serialize(product), product);
        }

        return ("Either product name or SKU must be provided", null);
    }

    private async Task<(string result, ProductResponse? affectedProduct)> ExecuteListProducts(
        JsonNode? input,
        IProductService productService,
        CancellationToken cancellationToken)
    {
        var searchTerm = input?["search_term"]?.GetValue<string>();

        List<ProductResponse> products;
        if (string.IsNullOrEmpty(searchTerm))
        {
            products = await productService.GetAllProductsAsync(cancellationToken);
        }
        else
        {
            products = await productService.SearchProductsAsync(searchTerm, cancellationToken);
        }

        var result = products.Count == 0
            ? "No products found"
            : JsonSerializer.Serialize(products);

        // For list operations, return the first product if available (or null)
        return (result, products.FirstOrDefault());
    }

    private async Task<(string result, ProductResponse? affectedProduct)> ExecuteCreateProduct(
        JsonNode? input,
        IProductService productService,
        CancellationToken cancellationToken)
    {
        if (input == null) return ("Product data is required", null);

        var request = new ProductRequest
        {
            Name = input["name"]?.GetValue<string>() ?? string.Empty,
            Description = input["description"]?.GetValue<string>() ?? string.Empty,
            Price = input["price"]?.GetValue<decimal>() ?? 0,
            Sku = input["sku"]?.GetValue<string>() ?? string.Empty
        };

        // Validate required fields
        if (string.IsNullOrWhiteSpace(request.Name))
            return ("Name is required", null);

        if (string.IsNullOrWhiteSpace(request.Description))
            return ("Description is required", null);

        if (request.Price <= 0)
            return ("Price is required and must be greater than 0", null);

        if (string.IsNullOrWhiteSpace(request.Sku))
            return ("SKU is required", null);

        try
        {
            var createdProduct = await productService.CreateProductAsync(request, cancellationToken);
            return (JsonSerializer.Serialize(createdProduct), createdProduct);
        }
        catch (InvalidOperationException ex)
        {
            return (ex.Message, null);
        }
    }

    private async Task<(string result, ProductResponse? affectedProduct)> ExecuteUpdateProduct(
        JsonNode? input,
        IProductService productService,
        CancellationToken cancellationToken)
    {
        if (input == null) return ("Product data is required", null);

        var identifier = input["identifier"]?.GetValue<string>();
        var isSku = input["is_sku"]?.GetValue<bool>() ?? false;

        if (string.IsNullOrEmpty(identifier))
            return ("Product identifier (name or SKU) is required", null);

        var request = new ProductRequest
        {
            Name = input["name"]?.GetValue<string>() ?? string.Empty,
            Description = input["description"]?.GetValue<string>() ?? string.Empty,
            Price = input["price"]?.GetValue<decimal>() ?? 0,
            Sku = input["sku"]?.GetValue<string>() ?? string.Empty
        };

        // Validate required fields
        if (string.IsNullOrWhiteSpace(request.Name))
            return ("Name is required", null);

        if (string.IsNullOrWhiteSpace(request.Description))
            return ("Description is required", null);

        if (request.Price <= 0)
            return ("Price must be greater than 0", null);

        if (string.IsNullOrWhiteSpace(request.Sku))
            return ("SKU is required", null);

        try
        {
            var updatedProduct = await productService.UpdateProductAsync(identifier, request, isSku, cancellationToken);

            if (updatedProduct == null)
            {
                var identifierType = isSku ? "SKU" : "name";
                return ($"Product with {identifierType} '{identifier}' not found", null);
            }

            return (JsonSerializer.Serialize(updatedProduct), updatedProduct);
        }
        catch (InvalidOperationException ex)
        {
            return (ex.Message, null);
        }
    }

    private async Task<(string result, ProductResponse? affectedProduct)> ExecuteDeleteProduct(
        JsonNode? input,
        IProductService productService,
        CancellationToken cancellationToken)
    {
        if (input == null) return ("Product name or SKU is required", null);

        var name = input["name"]?.GetValue<string>();
        var sku = input["sku"]?.GetValue<string>();

        if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(sku))
            return ("Either product name or SKU must be provided", null);

        try
        {
            // Get the product before deleting to return it as affected product
            ProductResponse? product = null;
            bool deleted = false;
            string identifier;

            if (!string.IsNullOrEmpty(name))
            {
                product = await productService.GetProductByNameAsync(name, cancellationToken);
                deleted = await productService.DeleteProductByNameAsync(name, cancellationToken);
                identifier = $"name '{name}'";
            }
            else
            {
                product = await productService.GetProductBySkuAsync(sku!, cancellationToken);
                deleted = await productService.DeleteProductBySkuAsync(sku!, cancellationToken);
                identifier = $"SKU '{sku}'";
            }

            var result = deleted
                ? $"Product with {identifier} has been deleted successfully"
                : $"Product with {identifier} not found";

            return (result, product);
        }
        catch (InvalidOperationException ex)
        {
            return (ex.Message, null);
        }
    }
}
