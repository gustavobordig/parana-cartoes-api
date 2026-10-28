using Cartoes.Application.Abstractions;

namespace Cartoes.Application.Cartoes.Listar;

public sealed class ListarCartoesHandler(IEmissaoCartoesRepository repositorio)
{
    public async Task<IReadOnlyList<CartaoResponse>> HandleAsync(Guid clienteId, CancellationToken cancellationToken)
    {
        var emissoes = await repositorio.ListarPorClienteAsync(clienteId, cancellationToken);

        return emissoes
            .SelectMany(emissao => emissao.Cartoes.Select(cartao => new CartaoResponse(
                cartao.Id,
                emissao.PropostaId,
                cartao.NumeroMascarado,
                cartao.Limite,
                emissao.EmitidoEm)))
            .ToList();
    }
}
