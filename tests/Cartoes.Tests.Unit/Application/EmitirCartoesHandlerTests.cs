using Cartoes.Application.Abstractions;
using Cartoes.Application.Cartoes.Emitir;
using Cartoes.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using ParanaBanco.Credito.Contracts;

namespace Cartoes.Tests.Unit.Application;

public sealed class EmitirCartoesHandlerTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 28, 17, 0, 0, TimeSpan.Zero);

    private readonly IEmissaoCartoesRepository _repositorio = Substitute.For<IEmissaoCartoesRepository>();
    private readonly EmitirCartoesHandler _handler;

    public EmitirCartoesHandlerTests()
    {
        _repositorio.AdicionarAsync(Arg.Any<EmissaoCartoes>(), Arg.Any<CancellationToken>()).Returns(true);

        _handler = new EmitirCartoesHandler(
            _repositorio,
            new FakeTimeProvider(Agora),
            new Random(42),
            NullLogger<EmitirCartoesHandler>.Instance);
    }

    private static PropostaGerada Proposta(string status, int quantidade, decimal limite) =>
        new(Guid.NewGuid(), Guid.NewGuid(), 700, status, limite, quantidade, Agora);

    [Fact]
    public async Task HandleAsync_PropostaAprovadaComDoisCartoes_EmiteESalvaDoisCartoes()
    {
        var evento = Proposta("Aprovada", 2, 5_000m);

        await _handler.HandleAsync(evento, CancellationToken.None);

        await _repositorio.Received(1).AdicionarAsync(
            Arg.Is<EmissaoCartoes>(e =>
                e.PropostaId == evento.PropostaId &&
                e.ClienteId == evento.ClienteId &&
                e.Cartoes.Count == 2 &&
                e.LimiteTotal == 10_000m &&
                e.EmitidoEm == Agora),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_PropostaAprovadaComUmCartao_EmiteUmCartao()
    {
        await _handler.HandleAsync(Proposta("Aprovada", 1, 1_000m), CancellationToken.None);

        await _repositorio.Received(1).AdicionarAsync(
            Arg.Is<EmissaoCartoes>(e => e.Cartoes.Count == 1 && e.LimiteTotal == 1_000m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_PropostaNegada_NaoEmiteCartao()
    {
        await _handler.HandleAsync(Proposta("Negada", 0, 0m), CancellationToken.None);

        await _repositorio.DidNotReceive().ExisteParaPropostaAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _repositorio.DidNotReceive().AdicionarAsync(Arg.Any<EmissaoCartoes>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_CartoesJaEmitidos_IgnoraEventoRepetido()
    {
        var evento = Proposta("Aprovada", 2, 5_000m);
        _repositorio.ExisteParaPropostaAsync(evento.PropostaId, Arg.Any<CancellationToken>()).Returns(true);

        await _handler.HandleAsync(evento, CancellationToken.None);

        await _repositorio.DidNotReceive().AdicionarAsync(Arg.Any<EmissaoCartoes>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_EmissaoConcorrenteJaGravada_NaoLancaExcecao()
    {
        _repositorio.AdicionarAsync(Arg.Any<EmissaoCartoes>(), Arg.Any<CancellationToken>()).Returns(false);

        var excecao = await Record.ExceptionAsync(() =>
            _handler.HandleAsync(Proposta("Aprovada", 2, 5_000m), CancellationToken.None));

        Assert.Null(excecao);
    }
}
