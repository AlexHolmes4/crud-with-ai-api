using CrudApi.Domain.Repositories;
using CrudApi.Infrastructure.Data;
using CrudApi.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CrudApi.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services)
    {
        // Register DbContext with in-memory database
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase("CrudApiDb"));

        // Register repositories
        services.AddScoped<IProductRepository, ProductRepository>();

        return services;
    }
}
