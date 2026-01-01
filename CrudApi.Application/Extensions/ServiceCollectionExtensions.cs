using CrudApi.Application.Mappings;
using CrudApi.Application.Services;
using Mapster;
using Microsoft.Extensions.DependencyInjection;

namespace CrudApi.Application.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IProductService, ProductService>();
        services.AddSingleton<IAnthropicToolService, AnthropicToolService>();
        services.AddSingleton<IConversationService, ConversationService>();

        // Configure Mapster
        var config = TypeAdapterConfig.GlobalSettings;
        new ProductMappingConfig().Register(config);

        return services;
    }
}
