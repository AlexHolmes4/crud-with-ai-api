using CrudApi.Domain.Entities;
using CrudApi.Domain.Repositories;
using CrudApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CrudApi.Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly ApplicationDbContext _dbContext;

    public ProductRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<List<Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Products
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Product>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        var lowerTerm = searchTerm.ToLower();
        return await _dbContext.Products
            .Where(p => p.Name.ToLower().Contains(lowerTerm) 
                || (p.Description != null && p.Description.ToLower().Contains(lowerTerm))
                || (p.Sku != null && p.Sku.ToLower().Contains(lowerTerm)))
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Product> CreateAsync(Product product, CancellationToken cancellationToken = default)
    {
        product.CreatedAt = DateTime.UtcNow;
        
        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync(cancellationToken);
        
        return product;
    }

    public async Task<Product?> UpdateAsync(Product product, CancellationToken cancellationToken = default)
    {
        var existing = await GetByIdAsync(product.Id, cancellationToken);
        if (existing == null)
            return null;

        existing.Name = product.Name;
        existing.Description = product.Description;
        existing.Price = product.Price;
        existing.Sku = product.Sku;
        existing.UpdatedAt = DateTime.UtcNow;

        _dbContext.Products.Update(existing);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return existing;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await GetByIdAsync(id, cancellationToken);
        if (product == null)
            return false;

        _dbContext.Products.Remove(product);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
