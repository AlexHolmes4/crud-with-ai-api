using CrudApi.Application.Dtos;
using CrudApi.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CrudApi.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    public async Task<ActionResult<List<ProductResponse>>> GetAllProducts(CancellationToken cancellationToken)
    {
        var products = await _productService.GetAllProductsAsync(cancellationToken);
        return Ok(products);
    }

    [HttpGet("name/{name}")]
    public async Task<ActionResult<ProductResponse>> GetProductByName(string name, CancellationToken cancellationToken)
    {
        var product = await _productService.GetProductByNameAsync(name, cancellationToken);
        if (product == null)
            return NotFound(new { message = $"Product with name '{name}' not found" });

        return Ok(product);
    }

    [HttpGet("sku/{sku}")]
    public async Task<ActionResult<ProductResponse>> GetProductBySku(string sku, CancellationToken cancellationToken)
    {
        var product = await _productService.GetProductBySkuAsync(sku, cancellationToken);
        if (product == null)
            return NotFound(new { message = $"Product with SKU '{sku}' not found" });

        return Ok(product);
    }

    [HttpGet("search/{searchTerm}")]
    public async Task<ActionResult<List<ProductResponse>>> SearchProducts(string searchTerm, CancellationToken cancellationToken)
    {
        var products = await _productService.SearchProductsAsync(searchTerm, cancellationToken);
        return Ok(products);
    }

    [HttpPost]
    public async Task<ActionResult<ProductResponse>> CreateProduct(
        [FromBody] ProductRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var product = await _productService.CreateProductAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetProductByName), new { name = product.Name }, product);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("name/{name}")]
    public async Task<ActionResult<ProductResponse>> UpdateProductByName(
        string name,
        [FromBody] ProductRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var product = await _productService.UpdateProductAsync(name, request, false, cancellationToken);
            if (product == null)
                return NotFound(new { message = $"Product with name '{name}' not found" });

            return Ok(product);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("sku/{sku}")]
    public async Task<ActionResult<ProductResponse>> UpdateProductBySku(
        string sku,
        [FromBody] ProductRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var product = await _productService.UpdateProductAsync(sku, request, true, cancellationToken);
            if (product == null)
                return NotFound(new { message = $"Product with SKU '{sku}' not found" });

            return Ok(product);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("name/{name}")]
    public async Task<IActionResult> DeleteProductByName(string name, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await _productService.DeleteProductByNameAsync(name, cancellationToken);
            if (!deleted)
                return NotFound(new { message = $"Product with name '{name}' not found" });

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("sku/{sku}")]
    public async Task<IActionResult> DeleteProductBySku(string sku, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await _productService.DeleteProductBySkuAsync(sku, cancellationToken);
            if (!deleted)
                return NotFound(new { message = $"Product with SKU '{sku}' not found" });

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
