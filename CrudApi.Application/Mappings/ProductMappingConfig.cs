using CrudApi.Application.Dtos;
using CrudApi.Domain.Entities;
using Mapster;

namespace CrudApi.Application.Mappings;

public class ProductMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Product, ProductResponse>();

        config.NewConfig<ProductRequest, Product>()
            .Map(dest => dest.CreatedAt, src => DateTime.UtcNow);
    }
}
