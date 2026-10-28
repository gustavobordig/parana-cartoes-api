using Cartoes.Application.Cartoes.Emitir;
using Cartoes.Application.Cartoes.Listar;
using Microsoft.Extensions.DependencyInjection;

namespace Cartoes.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(Random.Shared);
        services.AddScoped<EmitirCartoesHandler>();
        services.AddScoped<ListarCartoesHandler>();
        return services;
    }
}
