using CrudApi.Application.Dtos;
using CrudApi.Domain.Entities;
using CrudApi.Domain.Repositories;
using Mapster;

namespace CrudApi.Application.Services;

public interface IProductService
{
    Task<ProductResponse?> GetProductByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<ProductResponse?> GetProductBySkuAsync(string sku, CancellationToken cancellationToken = default);
    Task<List<ProductResponse>> GetAllProductsAsync(CancellationToken cancellationToken = default);
    Task<List<ProductResponse>> SearchProductsAsync(string searchTerm, CancellationToken cancellationToken = default);
    Task<List<ProductResponse>> GetProductsByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<ProductResponse> CreateProductAsync(ProductRequest request, CancellationToken cancellationToken = default);
    Task<ProductResponse?> UpdateProductAsync(string identifier, ProductRequest request, bool isSku = false, CancellationToken cancellationToken = default);
    Task<bool> DeleteProductByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<bool> DeleteProductBySkuAsync(string sku, CancellationToken cancellationToken = default);
}

public class ProductService : IProductService
{
    private readonly IProductRepository _repository;

    public ProductService(IProductRepository repository)
    {
        _repository = repository;
    }

    public async Task<ProductResponse?> GetProductByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var products = await _repository.SearchAsync(name, cancellationToken);
        var matchingProducts = products.Where(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();

        // Return single exact match, null if multiple or none
        return matchingProducts.Count == 1 ? matchingProducts[0].Adapt<ProductResponse>() : null;
    }

    public async Task<List<ProductResponse>> GetProductsByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var products = await _repository.SearchAsync(name, cancellationToken);
        var matchingProducts = products.Where(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
        return matchingProducts.Adapt<List<ProductResponse>>();
    }

    public async Task<ProductResponse?> GetProductBySkuAsync(string sku, CancellationToken cancellationToken = default)
    {
        var products = await _repository.SearchAsync(sku, cancellationToken);
        var product = products.FirstOrDefault(p => p.Sku != null && p.Sku.Equals(sku, StringComparison.OrdinalIgnoreCase));
        return product?.Adapt<ProductResponse>();
    }

    public async Task<List<ProductResponse>> GetAllProductsAsync(CancellationToken cancellationToken = default)
    {
        var products = await _repository.GetAllAsync(cancellationToken);
        return products.Adapt<List<ProductResponse>>();
    }

    public async Task<List<ProductResponse>> SearchProductsAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        var products = await _repository.SearchAsync(searchTerm, cancellationToken);
        return products.Adapt<List<ProductResponse>>();
    }

    public async Task<ProductResponse> CreateProductAsync(ProductRequest request, CancellationToken cancellationToken = default)
    {
        // Check for duplicate SKU (required and must be unique)
        var existingBySku = await GetProductBySkuAsync(request.Sku, cancellationToken);
        if (existingBySku != null)
            throw new InvalidOperationException($"A product with the SKU '{request.Sku}' already exists. Please use a different SKU.");

        var product = request.Adapt<Product>();
        var createdProduct = await _repository.CreateAsync(product, cancellationToken);
        return createdProduct.Adapt<ProductResponse>();
    }

    public async Task<ProductResponse?> UpdateProductAsync(string identifier, ProductRequest request, bool isSku = false, CancellationToken cancellationToken = default)
    {
        // Find the existing product by SKU or Name
        Product? existingProduct = null;

        if (isSku)
        {
            var products = await _repository.SearchAsync(identifier, cancellationToken);
            existingProduct = products.FirstOrDefault(p => p.Sku != null && p.Sku.Equals(identifier, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            // For name, check if there are multiple matches
            var products = await _repository.SearchAsync(identifier, cancellationToken);
            var matchingProducts = products.Where(p => p.Name.Equals(identifier, StringComparison.OrdinalIgnoreCase)).ToList();

            if (matchingProducts.Count > 1)
            {
                throw new InvalidOperationException($"Multiple products found with name '{identifier}'. Please specify the SKU to update the correct product.");
            }

            existingProduct = matchingProducts.FirstOrDefault();
        }

        if (existingProduct == null)
            return null;

        // Check if new SKU conflicts with another product
        if (request.Sku != existingProduct.Sku)
        {
            var existingBySku = await GetProductBySkuAsync(request.Sku, cancellationToken);
            if (existingBySku != null)
                throw new InvalidOperationException($"A product with the SKU '{request.Sku}' already exists. Please use a different SKU.");
        }

        // Update all fields
        existingProduct.Name = request.Name;
        existingProduct.Description = request.Description;
        existingProduct.Price = request.Price;
        existingProduct.Sku = request.Sku;
        existingProduct.UpdatedAt = DateTime.UtcNow;

        var updatedProduct = await _repository.UpdateAsync(existingProduct, cancellationToken);
        return updatedProduct?.Adapt<ProductResponse>();
    }

    public async Task<bool> DeleteProductByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var products = await _repository.SearchAsync(name, cancellationToken);
        var matchingProducts = products.Where(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();

        if (matchingProducts.Count == 0)
            return false;

        if (matchingProducts.Count > 1)
            throw new InvalidOperationException($"Multiple products found with name '{name}'. Please specify the SKU to delete the correct product.");

        return await _repository.DeleteAsync(matchingProducts[0].Id, cancellationToken);
    }

    public async Task<bool> DeleteProductBySkuAsync(string sku, CancellationToken cancellationToken = default)
    {
        var products = await _repository.SearchAsync(sku, cancellationToken);
        var product = products.FirstOrDefault(p => p.Sku != null && p.Sku.Equals(sku, StringComparison.OrdinalIgnoreCase));

        if (product == null)
            return false;

        return await _repository.DeleteAsync(product.Id, cancellationToken);
    }
}
