using Cartoes.Application.Abstractions;
using Cartoes.Application.Cartoes.Listar;
using Cartoes.Domain.Entities;
using NSubstitute;

namespace Cartoes.Tests.Unit.Application;

public sealed class ListarCartoesHandlerTests
{
    private readonly IEmissaoCartoesRepository _repositorio = Substitute.For<IEmissaoCartoesRepository>();
    private readonly ListarCartoesHandler _handler;

    public ListarCartoesHandlerTests()
    {
        _handler = new ListarCartoesHandler(_repositorio);
    }

    [Fact]
    public async Task HandleAsync_ClienteComCartoes_RetornaCartoesComNumeroMascarado()
    {
        var clienteId = Guid.NewGuid();
        var emissao = EmissaoCartoes.Emitir(Guid.NewGuid(), clienteId, 2, 5_000m, DateTimeOffset.UtcNow, new Random(3));
        _repositorio.ListarPorClienteAsync(clienteId, Arg.Any<CancellationToken>()).Returns([emissao]);

        var cartoes = await _handler.HandleAsync(clienteId, CancellationToken.None);

        Assert.Equal(2, cartoes.Count);
        Assert.All(cartoes, c =>
        {
            Assert.StartsWith("**** **** **** ", c.Numero);
            Assert.Equal(emissao.PropostaId, c.PropostaId);
            Assert.Equal(5_000m, c.Limite);
        });
    }

    [Fact]
    public async Task HandleAsync_ClienteSemCartoes_RetornaListaVazia()
    {
        _repositorio.ListarPorClienteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);

        var cartoes = await _handler.HandleAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Empty(cartoes);
    }
}
