using Cartoes.Domain.Common;
using Cartoes.Domain.Entities;

namespace Cartoes.Tests.Unit.Domain;

public sealed class EmissaoCartoesTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 28, 17, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Emitir_UmCartao_EmiteComLimiteDeMil()
    {
        var emissao = EmissaoCartoes.Emitir(Guid.NewGuid(), Guid.NewGuid(), 1, 1_000m, Agora, new Random(42));

        var cartao = Assert.Single(emissao.Cartoes);
        Assert.Equal(1_000m, cartao.Limite);
        Assert.Equal(1_000m, emissao.LimiteTotal);
    }

    [Fact]
    public void Emitir_DoisCartoes_AplicaLimiteIndividualSomandoDezMil()
    {
        var emissao = EmissaoCartoes.Emitir(Guid.NewGuid(), Guid.NewGuid(), 2, 5_000m, Agora, new Random(42));

        Assert.Equal(2, emissao.Cartoes.Count);
        Assert.All(emissao.Cartoes, c => Assert.Equal(5_000m, c.Limite));
        Assert.Equal(10_000m, emissao.LimiteTotal);
    }

    [Fact]
    public void Emitir_DoisCartoes_GeraFinaisDistintosComQuatroDigitos()
    {
        var emissao = EmissaoCartoes.Emitir(Guid.NewGuid(), Guid.NewGuid(), 2, 5_000m, Agora, new Random(7));

        Assert.All(emissao.Cartoes, c => Assert.Matches("^[0-9]{4}$", c.NumeroFinal));
        Assert.Equal(2, emissao.Cartoes.Select(c => c.NumeroFinal).Distinct().Count());
        Assert.Equal(2, emissao.Cartoes.Select(c => c.Id).Distinct().Count());
    }

    [Fact]
    public void Emitir_AleatorioQueRepeteONumero_AindaGeraFinaisDistintos()
    {
        var aleatorio = new SequenciaFixa(1234, 1234, 5678);

        var emissao = EmissaoCartoes.Emitir(Guid.NewGuid(), Guid.NewGuid(), 2, 5_000m, Agora, aleatorio);

        Assert.Equal(["1234", "5678"], emissao.Cartoes.Select(c => c.NumeroFinal));
    }

    [Fact]
    public void Emitir_NumeroPequeno_CompletaComZerosAEsquerda()
    {
        var emissao = EmissaoCartoes.Emitir(Guid.NewGuid(), Guid.NewGuid(), 1, 1_000m, Agora, new SequenciaFixa(7));

        Assert.Equal("**** **** **** 0007", emissao.Cartoes[0].NumeroMascarado);
    }

    [Fact]
    public void Emitir_DadosDaProposta_PreservaIdentificadores()
    {
        var propostaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();

        var emissao = EmissaoCartoes.Emitir(propostaId, clienteId, 1, 1_000m, Agora, new Random(1));

        Assert.Equal(propostaId, emissao.PropostaId);
        Assert.Equal(clienteId, emissao.ClienteId);
        Assert.Equal(Agora, emissao.EmitidoEm);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-1)]
    public void Emitir_QuantidadeForaDoPermitido_LancaDomainException(int quantidade)
    {
        Assert.Throws<DomainException>(() =>
            EmissaoCartoes.Emitir(Guid.NewGuid(), Guid.NewGuid(), quantidade, 1_000m, Agora, new Random(1)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Emitir_LimiteNaoPositivo_LancaDomainException(decimal limite)
    {
        Assert.Throws<DomainException>(() =>
            EmissaoCartoes.Emitir(Guid.NewGuid(), Guid.NewGuid(), 1, limite, Agora, new Random(1)));
    }

    private sealed class SequenciaFixa(params int[] valores) : Random
    {
        private int _indice;

        public override int Next(int minValue, int maxValue) => valores[_indice++];
    }
}
