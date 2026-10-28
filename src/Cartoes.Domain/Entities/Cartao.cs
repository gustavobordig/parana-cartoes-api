namespace Cartoes.Domain.Entities;

public sealed class Cartao
{
    public Guid Id { get; }
    public string NumeroFinal { get; }
    public decimal Limite { get; }

    public string NumeroMascarado => $"**** **** **** {NumeroFinal}";

    public Cartao(Guid id, string numeroFinal, decimal limite)
    {
        Id = id;
        NumeroFinal = numeroFinal;
        Limite = limite;
    }
}
