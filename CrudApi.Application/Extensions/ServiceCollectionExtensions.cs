using Anthropic.SDK;
using CrudApi.Application.Mappings;
using CrudApi.Application.Services;
using Mapster;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CrudApi.Application.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        IConfiguration configuration,
        string? anthropicApiKey = null)
    {
        // Register Anthropic client
        // Priority: 1. Parameter, 2. Configuration, 3. Environment Variable
        var apiKey = anthropicApiKey
            ?? configuration["Anthropic:ApiKey"]
            ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException(
                "Anthropic API key is required. Set it via:\n" +
                "1. appsettings.json: \"Anthropic:ApiKey\"\n" +
                "2. Environment variable: ANTHROPIC_API_KEY\n" +
                "3. User secrets (for development)");

        services.AddSingleton(new AnthropicClient(apiKey));

        // Register services
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IAnthropicToolService, AnthropicToolService>();
        services.AddScoped<IConversationService, ConversationService>();

        // Configure Mapster
        var config = TypeAdapterConfig.GlobalSettings;
        new ProductMappingConfig().Register(config);

        return services;
    }
}
