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

        // Get tools for Claude
        var tools = _toolService.GetProductTools();

        // Create message parameters with system prompt
        var parameters = new MessageParameters
        {
            Messages = messages,
            MaxTokens = 2048,
            Model = AnthropicModels.Claude35Haiku,
            Stream = false,
            Temperature = 0.7m,
            Tools = tools,
            System = new List<SystemMessage>
            {
                new SystemMessage(GetSystemPrompt())
            }
        };

        // Call Claude
        var claudeResponse = await _anthropicClient.Messages.GetClaudeMessageAsync(parameters);

        // Add assistant response to conversation
        messages.Add(claudeResponse.Message);

        // Track last action and affected product for metadata
        string? lastAction = null;
        ProductResponse? lastAffectedProduct = null;

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

                // Track the last action and affected product
                lastAction = toolCall.Name;
                lastAffectedProduct = affectedProduct;

                // Add tool result to conversation using the proper SDK constructor
                messages.Add(new Message(toolCall, toolResult));
            }

            // Get follow-up response from Claude after tool execution
            var followUpResponse = await _anthropicClient.Messages.GetClaudeMessageAsync(parameters);
            messages.Add(followUpResponse.Message);
        }

        // Convert messages to DTOs, filtering out empty messages
        var filteredMessages = messages
            .Where(m => m.Role == RoleType.User || m.Role == RoleType.Assistant)
            .Where(m => !string.IsNullOrWhiteSpace(ExtractMessageContent(m)))
            .ToList();

        var chatMessages = filteredMessages
            .Select((m, index) => new ChatMessageResponse
            {
                Role = m.Role.ToString().ToLower(),
                Content = ExtractMessageContent(m),
                // Only apply metadata to the last assistant message (the final response)
                ProcessedAction = (m.Role == RoleType.Assistant && index == filteredMessages.Count - 1) ? lastAction : null,
                AffectedProduct = (m.Role == RoleType.Assistant && index == filteredMessages.Count - 1) ? lastAffectedProduct : null
            })
            .ToList();

        var response = new ChatPromptResponse
        {
            ConversationId = conversationId,
            Messages = chatMessages
        };

        return response;
    }

    private static string ExtractMessageContent(Message message)
    {
        if (message?.Content == null || message.Content.Count == 0)
            return string.Empty;

        var textContents = new List<string>();

        foreach (var content in message.Content)
        {
            if (content is TextContent textContent)
            {
                textContents.Add(textContent.Text);
            }
        }

        return string.Join("\n", textContents);
    }

    private static string GetSystemPrompt()
    {
        return @"You are a AI assistant for a product management system. 

## Available Operations
1. find_product - Find a product by Name or SKU
2. list_products - List all products or search by term
3. create_product - Create a new product
4. update_product - Update an existing product by Name or SKU
5. delete_product - Delete a product by Name or SKU

⚠️ CRITICAL INSTRUCTION ⚠️
When you call a function/tool, you will receive a JSON response. You MUST read and parse this JSON response to extract values. NEVER make up, generate, or guess values. ALWAYS use the exact values from the tool response you received.

CRITICAL find_product workflow:
1. When a user requests to find a product, use the find_product tool with either Name or SKU.
2. If multiple products match the Name, inform the user and ask for the SKU to identify the specific product.
3. Present the found product using the Product Display Format detailed below in this message.
4. if no product is found, inform the user and offer to list all products or create a new one.

CRITICAL list_products workflow:
1. When a user requests to list products, use the list_products tool.
2. List all products using the Product Display Format detailed below in this message.
3. if no products exist, inform the user and offer to create a new one.

CRITICAL create_product workflow:
1. When a user requests to create a product you must collect the following required fields:
    - Name
    - Description
    - Price
    - SKU
2. If any required field is missing, ask the user specifically for that information.
3. Before calling the create_product tool, display the information using the Product Display Format and ask them to confirm the details are correct. Show: **Product: [Name]** - Description: [Description] - Price: $[Price] - SKU: [SKU]. Then ask ""Does this look correct?""
4. WAIT for the user to explicitly confirm (e.g., ""yes"", ""confirm"", ""proceed"", ""create it"") BEFORE calling the tool.
5. ONLY AFTER explicit user confirmation should you call the create_product tool.
6. NEVER call create_product without explicit user confirmation.
7. When creating if the product_create fails because the SKU already exists, inform the user and ask for a different SKU to try again. Follow the same confirmation process before retrying.

CRITICAL update_product workflow:
1. When a user requests to update a product, you must identify the product by either Name or SKU.
2. If multiple products match the Name, inform the user and ask for the SKU to identify the specific product.
3. Collect the fields the user wants to update:
    - Name
    - Description
    - Price
    - SKU
4. If any required field is missing, ask the user specifically for that information.
5. Before calling the update_product tool, display the updated information using the Product Display Format and ask them to confirm the details are correct. Show: **Product: [Name]** - Description: [Description] - Price: $[Price] - SKU: [SKU]. Then ask ""Does this look correct?""
6. WAIT for the user to explicitly confirm (e.g., ""yes"", ""confirm"", ""proceed"", ""update it"") BEFORE calling the tool.
7. ONLY AFTER explicit user confirmation should you call the update_product tool.
8. NEVER call update_product without explicit user confirmation.
9. When updating if the product_update fails because the SKU already exists, inform the user and ask for a different SKU to try again. Follow the same confirmation process before retrying.

CRITICAL delete_product workflow:
1. When a user requests to delete a product, you must identify the product by either Name or SKU.
2. If multiple products match the Name, inform the user and ask for the SKU to identify the specific product.
3. Before calling the delete_product tool, display the product details using the Product Display Format and ask them to confirm they want to delete it. Show: **Product: [Name]** - Description: [Description] - Price: $[Price] - SKU: [SKU]. Then ask ""Are you absolutely sure you want to delete this product? This action cannot be undone.""
4. WAIT for the user to explicitly confirm (e.g., ""yes"", ""confirm"", ""delete it"", ""proceed"") BEFORE calling the tool.
5. ONLY AFTER explicit user confirmation should you call the delete_product tool.
6. NEVER call delete_product without explicit user confirmation.

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

## Guidelines
1. ALWAYS use the exact display format shown above when presenting products
2. Users can ONLY find/update/delete products by Name or SKU, never by ID
3. When users ask to ""add"", ""create"", or ""make"" a product, follow the create_product workflow
4. When users ask to ""show"", ""find"", ""get"", or ""display"" follow the find_product workflow
5. When users ask to ""change"", ""update"", or ""modify"" follow the update_product workflow
6. When users ask to ""remove"", ""delete"", or ""get rid of"" follow the delete_product workflow
7. If a user's request is ambiguous or missing required fields, ask clarifying questions
8. Always confirm destructive actions (updates and deletes) by showing the product details first
9. Always confirm the provided information before creating or updating a product
11. When listing multiple products, use the same format for each one

## Important Rules
- NEVER expose or reference product IDs to users
- Be conversational but consistent in formatting";
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
