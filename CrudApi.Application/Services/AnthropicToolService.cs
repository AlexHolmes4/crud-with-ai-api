using Anthropic.SDK.Common;
using System.Text.Json.Nodes;

namespace CrudApi.Application.Services;

/// <summary>
/// Service that defines tools for Claude to interact with the Product CRUD operations.
/// </summary>
public interface IAnthropicToolService
{
    List<Tool> GetProductTools();
}

public class AnthropicToolService : IAnthropicToolService
{
    public List<Tool> GetProductTools()
    {
        return new List<Tool>
        {
            CreateFindProductTool(),
            CreateListProductsTool(),
            CreateCreateProductTool(),
            CreateUpdateProductTool(),
            CreateDeleteProductTool()
        };
    }

    private static Tool CreateFindProductTool()
    {
        var inputSchema = new
        {
            type = "object",
            properties = new
            {
                name = new
                {
                    type = "string",
                    description = "The name of the product to find (exact match)"
                },
                sku = new
                {
                    type = "string",
                    description = "The SKU of the product to find"
                }
            },
            required = new string[] { }
        };

        return new Function(
            name: "find_product",
            description: "Find and retrieve a specific product by its Name or SKU. At least one must be provided.",
            parameters: JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(inputSchema))
        );
    }

    private static Tool CreateListProductsTool()
    {
        var inputSchema = new
        {
            type = "object",
            properties = new
            {
                search_term = new
                {
                    type = "string",
                    description = "Optional search term to filter products by name or description"
                }
            },
            required = new string[] { }
        };

        return new Function(
            name: "list_products",
            description: "List all products or search products by name/description",
            parameters: JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(inputSchema))
        );
    }

    private static Tool CreateCreateProductTool()
    {
        var inputSchema = new
        {
            type = "object",
            properties = new
            {
                name = new
                {
                    type = "string",
                    description = "The name of the product"
                },
                description = new
                {
                    type = "string",
                    description = "A detailed description of the product"
                },
                price = new
                {
                    type = "number",
                    description = "The price of the product"
                },
                sku = new
                {
                    type = "string",
                    description = "The SKU (Stock Keeping Unit) of the product"
                }
            },
            required = new[] { "name", "description", "price", "sku" }
        };

        return new Function(
            name: "create_product",
            description: "Create a new product with all required details (name, description, price, sku). SKU must be unique.",
            parameters: JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(inputSchema))
        );
    }

    private static Tool CreateUpdateProductTool()
    {
        var inputSchema = new
        {
            type = "object",
            properties = new
            {
                identifier = new
                {
                    type = "string",
                    description = "The current name or SKU of the product to update"
                },
                is_sku = new
                {
                    type = "boolean",
                    description = "Set to true if identifier is a SKU, false if it's a name"
                },
                name = new
                {
                    type = "string",
                    description = "The new name for the product"
                },
                description = new
                {
                    type = "string",
                    description = "The new description of the product"
                },
                price = new
                {
                    type = "number",
                    description = "The new price of the product"
                },
                sku = new
                {
                    type = "string",
                    description = "The new SKU for the product (must be unique)"
                }
            },
            required = new[] { "identifier", "name", "description", "price", "sku" }
        };

        return new Function(
            name: "update_product",
            description: "Update an existing product identified by Name or SKU. All product fields must be provided. If multiple products have the same name, the user will be asked to provide the SKU.",
            parameters: JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(inputSchema))
        );
    }

    private static Tool CreateDeleteProductTool()
    {
        var inputSchema = new
        {
            type = "object",
            properties = new
            {
                name = new
                {
                    type = "string",
                    description = "The name of the product to delete"
                },
                sku = new
                {
                    type = "string",
                    description = "The SKU of the product to delete"
                }
            },
            required = new string[] { }
        };

        return new Function(
            name: "delete_product",
            description: "Delete a product by its Name or SKU. At least one must be provided.",
            parameters: JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(inputSchema))
        );
    }
}
