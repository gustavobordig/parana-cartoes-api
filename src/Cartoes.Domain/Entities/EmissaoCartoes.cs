using Cartoes.Domain.Common;

namespace Cartoes.Domain.Entities;

public sealed class EmissaoCartoes
{
    public const int QuantidadeMaximaCartoes = 2;

    public Guid PropostaId { get; }
    public Guid ClienteId { get; }
    public DateTimeOffset EmitidoEm { get; }
    public IReadOnlyList<Cartao> Cartoes { get; }

    public decimal LimiteTotal => Cartoes.Sum(c => c.Limite);

    public EmissaoCartoes(Guid propostaId, Guid clienteId, DateTimeOffset emitidoEm, IReadOnlyList<Cartao> cartoes)
    {
        PropostaId = propostaId;
        ClienteId = clienteId;
        EmitidoEm = emitidoEm;
        Cartoes = cartoes;
    }

    public static EmissaoCartoes Emitir(
        Guid propostaId,
        Guid clienteId,
        int quantidade,
        decimal limitePorCartao,
        DateTimeOffset agora,
        Random aleatorio)
    {
        if (quantidade is < 1 or > QuantidadeMaximaCartoes)
            throw new DomainException($"Quantidade de cartões deve ser entre 1 e {QuantidadeMaximaCartoes}.");

        if (limitePorCartao <= 0)
            throw new DomainException("Limite por cartão deve ser maior que zero.");

        var finais = new HashSet<string>();
        while (finais.Count < quantidade)
            finais.Add(aleatorio.Next(0, 10_000).ToString("D4"));

        var cartoes = finais
            .Select(final => new Cartao(Guid.CreateVersion7(agora), final, limitePorCartao))
            .ToList();

        return new EmissaoCartoes(propostaId, clienteId, agora, cartoes);
    }
}
