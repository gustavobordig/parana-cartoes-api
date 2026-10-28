using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Cartoes.Infrastructure.Persistence;

internal sealed class MongoInicializador(
    IMongoCollection<EmissaoCartoesDocumento> colecao,
    ILogger<MongoInicializador> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var indice = new CreateIndexModel<EmissaoCartoesDocumento>(
            Builders<EmissaoCartoesDocumento>.IndexKeys.Ascending(d => d.ClienteId),
            new CreateIndexOptions { Name = "ix_clienteId" });

        try
        {
            await colecao.Indexes.CreateOneAsync(indice, cancellationToken: cancellationToken);
            logger.LogInformation("Índices do MongoDB verificados");
        }
        catch (Exception ex) when (ex is TimeoutException or MongoException)
        {
            logger.LogWarning(ex, "MongoDB indisponível no startup; índices não verificados");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
