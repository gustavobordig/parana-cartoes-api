using Cartoes.Application.Abstractions;
using Cartoes.Infrastructure.Messaging;
using Cartoes.Infrastructure.Messaging.Consumers;
using Cartoes.Infrastructure.Observability;
using Cartoes.Infrastructure.Persistence;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using ParanaBanco.Credito.Contracts;

namespace Cartoes.Infrastructure;

public static class DependencyInjection
{
    private const string Colecao = "emissoes_cartoes";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuracao)
    {
        var mongo = configuracao["Mongo:ConnectionString"]
            ?? throw new InvalidOperationException("Mongo:ConnectionString não configurada.");
        var nomeBanco = configuracao["Mongo:Database"] ?? "cartoes_db";

        var rabbitMq = RabbitMqOptions.DaUrl(configuracao["RabbitMq:Url"]
            ?? throw new InvalidOperationException("RabbitMq:Url não configurada."));

        services.AddSingleton<IMongoClient>(_ =>
        {
            var settings = MongoClientSettings.FromConnectionString(mongo);
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
            return new MongoClient(settings);
        });
        services.AddSingleton(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(nomeBanco));
        services.AddSingleton(sp => sp.GetRequiredService<IMongoDatabase>().GetCollection<EmissaoCartoesDocumento>(Colecao));
        services.AddHostedService<MongoInicializador>();

        services.AddScoped<IEmissaoCartoesRepository, EmissaoCartoesRepository>();
        services.AddScoped<CorrelationContext>();

        services.AddMassTransit(bus =>
        {
            bus.AddConsumer<PropostaGeradaConsumer, PropostaGeradaConsumerDefinition>();

            bus.UsingRabbitMq((contexto, rabbit) =>
            {
                rabbit.Host(rabbitMq.Host, rabbitMq.Porta, rabbitMq.VirtualHost, host =>
                {
                    host.Username(rabbitMq.Usuario);
                    host.Password(rabbitMq.Senha);
                });

                rabbit.Message<PropostaGerada>(m => m.SetEntityName("proposta-gerada"));

                rabbit.ConfigureEndpoints(contexto);
            });
        });

        services.AddHealthChecks().AddCheck<MongoHealthCheck>("mongodb");

        return services;
    }
}
