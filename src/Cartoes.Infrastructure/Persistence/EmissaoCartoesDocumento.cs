using Cartoes.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Cartoes.Infrastructure.Persistence;

internal sealed class EmissaoCartoesDocumento
{
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid PropostaId { get; init; }

    [BsonElement("clienteId")]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid ClienteId { get; init; }

    [BsonElement("emitidoEm")]
    public DateTime EmitidoEm { get; init; }

    [BsonElement("cartoes")]
    public List<CartaoDocumento> Cartoes { get; init; } = [];

    public static EmissaoCartoesDocumento De(EmissaoCartoes emissao) => new()
    {
        PropostaId = emissao.PropostaId,
        ClienteId = emissao.ClienteId,
        EmitidoEm = emissao.EmitidoEm.UtcDateTime,
        Cartoes = emissao.Cartoes.Select(CartaoDocumento.De).ToList()
    };

    public EmissaoCartoes ParaDominio() => new(
        PropostaId,
        ClienteId,
        new DateTimeOffset(DateTime.SpecifyKind(EmitidoEm, DateTimeKind.Utc)),
        Cartoes.Select(c => c.ParaDominio()).ToList());
}

internal sealed class CartaoDocumento
{
    [BsonElement("id")]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; init; }

    [BsonElement("numeroFinal")]
    public string NumeroFinal { get; init; } = null!;

    [BsonElement("limite")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Limite { get; init; }

    public static CartaoDocumento De(Cartao cartao) => new()
    {
        Id = cartao.Id,
        NumeroFinal = cartao.NumeroFinal,
        Limite = cartao.Limite
    };

    public Cartao ParaDominio() => new(Id, NumeroFinal, Limite);
}
