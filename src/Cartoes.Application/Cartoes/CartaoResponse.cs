namespace Cartoes.Application.Cartoes;

public sealed record CartaoResponse(
    Guid Id,
    Guid PropostaId,
    string Numero,
    decimal Limite,
    DateTimeOffset EmitidoEm);
