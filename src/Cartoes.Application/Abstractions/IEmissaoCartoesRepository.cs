using Cartoes.Domain.Entities;

namespace Cartoes.Application.Abstractions;

public interface IEmissaoCartoesRepository
{
    Task<bool> ExisteParaPropostaAsync(Guid propostaId, CancellationToken cancellationToken);

    Task<bool> AdicionarAsync(EmissaoCartoes emissao, CancellationToken cancellationToken);

    Task<IReadOnlyList<EmissaoCartoes>> ListarPorClienteAsync(Guid clienteId, CancellationToken cancellationToken);
}
