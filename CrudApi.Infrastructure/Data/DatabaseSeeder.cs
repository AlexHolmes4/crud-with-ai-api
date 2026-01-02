using CrudApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CrudApi.Infrastructure.Data;

public class DatabaseSeeder
{
    public static async Task SeedAsync(DbContext context, CancellationToken cancellationToken)
    {
        if (context is not ApplicationDbContext dbContext)
        {
            throw new InvalidOperationException(
                $"Expected {nameof(ApplicationDbContext)} but received {context.GetType().Name}.");
        }

        // Only seed if the database is empty
        if (dbContext.Products.Any())
        {
            return;
        }

        var products = new List<Product>
        {
            new Product
            {
                Id = 1,
                Name = "Wireless Headphones",
                Description = "High-quality wireless headphones with noise cancellation and 30-hour battery life.",
                Price = 199.99m,
                Sku = "WH-001",
                CreatedAt = DateTime.UtcNow
            },
            new Product
            {
                Id = 2,
                Name = "USB-C Cable",
                Description = "Durable USB-C cable with fast charging support. Works with multiple devices.",
                Price = 19.99m,
                Sku = "USB-C-001",
                CreatedAt = DateTime.UtcNow
            },
            new Product
            {
                Id = 3,
                Name = "Mechanical Keyboard",
                Description = "Premium mechanical keyboard with RGB lighting and mechanical switches.",
                Price = 149.99m,
                Sku = "MK-001",
                CreatedAt = DateTime.UtcNow
            },
            new Product
            {
                Id = 4,
                Name = "Wireless Mouse",
                Description = "Ergonomic wireless mouse with precision tracking and long battery life.",
                Price = 49.99m,
                Sku = "WM-001",
                CreatedAt = DateTime.UtcNow
            },
            new Product
            {
                Id = 5,
                Name = "Monitor Stand",
                Description = "Adjustable monitor stand with storage drawer for cables and accessories.",
                Price = 79.99m,
                Sku = "MS-001",
                CreatedAt = DateTime.UtcNow
            },
            new Product
            {
                Id = 6,
                Name = "Portable SSD",
                Description = "1TB portable SSD with USB-C connection. Perfect for fast file transfers and backups.",
                Price = 129.99m,
                Sku = "SSD-001",
                CreatedAt = DateTime.UtcNow
            }
        };

        await dbContext.Products.AddRangeAsync(products, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
