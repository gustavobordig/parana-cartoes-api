using Cartoes.Application.Abstractions;
using Cartoes.Application.Cartoes.Emitir;
using Cartoes.Domain.Entities;
using Cartoes.Infrastructure.Messaging;
using Cartoes.Infrastructure.Messaging.Consumers;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ParanaBanco.Credito.Contracts;

namespace Cartoes.Tests.Unit.Messaging;

public sealed class PropostaGeradaConsumerTests : IAsyncLifetime
{
    private readonly IEmissaoCartoesRepository _repositorio = Substitute.For<IEmissaoCartoesRepository>();
    private ServiceProvider _provider = null!;
    private ITestHarness _harness = null!;

    public async Task InitializeAsync()
    {
        _repositorio.AdicionarAsync(Arg.Any<EmissaoCartoes>(), Arg.Any<CancellationToken>()).Returns(true);

        _provider = new ServiceCollection()
            .AddLogging()
            .AddSingleton(_repositorio)
            .AddSingleton(TimeProvider.System)
            .AddSingleton(new Random(42))
            .AddScoped<EmitirCartoesHandler>()
            .AddMassTransitTestHarness(bus => bus.AddConsumer<PropostaGeradaConsumer>(consumer =>
                consumer.UseMessageRetry(retry => ConfiguracaoResiliencia.AplicarRetry(retry, TimeSpan.FromMilliseconds(10)))))
            .BuildServiceProvider(validateScopes: true);

        _harness = _provider.GetRequiredService<ITestHarness>();
        await _harness.Start();
    }

    public async Task DisposeAsync() => await _provider.DisposeAsync();

    private static PropostaGerada Proposta(string status, int quantidade, decimal limite) =>
        new(Guid.NewGuid(), Guid.NewGuid(), 700, status, limite, quantidade, DateTimeOffset.UtcNow);

    [Fact]
    public async Task Consume_PropostaAprovada_EmiteCartoes()
    {
        var evento = Proposta("Aprovada", 2, 5_000m);

        await _harness.Bus.Publish(evento);

        Assert.True(await _harness.Consumed.Any<PropostaGerada>());
        await _repositorio.Received(1).AdicionarAsync(
            Arg.Is<EmissaoCartoes>(e => e.PropostaId == evento.PropostaId && e.Cartoes.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_PropostaNegada_ConsomeSemEmitir()
    {
        await _harness.Bus.Publish(Proposta("Negada", 0, 0m));

        Assert.True(await _harness.Consumed.Any<PropostaGerada>());
        Assert.False(await _harness.Published.Any<Fault<PropostaGerada>>());
        await _repositorio.DidNotReceive().AdicionarAsync(Arg.Any<EmissaoCartoes>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_MongoIndisponivel_EsgotaRetryEGeraFault()
    {
        _repositorio.AdicionarAsync(Arg.Any<EmissaoCartoes>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("MongoDB indisponível"));

        await _harness.Bus.Publish(Proposta("Aprovada", 1, 1_000m));

        Assert.True(await _harness.Published.Any<Fault<PropostaGerada>>());
        await _repositorio.Received(1 + ConfiguracaoResiliencia.Tentativas)
            .AdicionarAsync(Arg.Any<EmissaoCartoes>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_QuantidadeInvalida_NaoRepeteEGeraFaultImediato()
    {
        await _harness.Bus.Publish(Proposta("Aprovada", 3, 5_000m));

        Assert.True(await _harness.Published.Any<Fault<PropostaGerada>>());
        await _repositorio.Received(1).ExisteParaPropostaAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
