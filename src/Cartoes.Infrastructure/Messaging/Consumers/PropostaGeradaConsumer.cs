using Cartoes.Application.Cartoes.Emitir;
using MassTransit;
using Microsoft.Extensions.Logging;
using ParanaBanco.Credito.Contracts;

namespace Cartoes.Infrastructure.Messaging.Consumers;

internal sealed class PropostaGeradaConsumer(
    EmitirCartoesHandler emitirCartoes,
    ILogger<PropostaGeradaConsumer> logger) : IConsumer<PropostaGerada>
{
    public async Task Consume(ConsumeContext<PropostaGerada> context)
    {
        var correlationId = context.CorrelationId ?? Guid.NewGuid();

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await emitirCartoes.HandleAsync(context.Message, context.CancellationToken);
        }
    }
}

internal sealed class PropostaGeradaConsumerDefinition : ConsumerDefinition<PropostaGeradaConsumer>
{
    public PropostaGeradaConsumerDefinition()
    {
        EndpointName = "cartao-proposta-gerada";
        ConcurrentMessageLimit = 8;
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<PropostaGeradaConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.PrefetchCount = 16;

        ConfiguracaoResiliencia.AplicarKillSwitch(endpointConfigurator);
        endpointConfigurator.UseMessageRetry(retry => ConfiguracaoResiliencia.AplicarRetry(retry));
    }
}
