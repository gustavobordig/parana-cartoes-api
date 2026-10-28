using Cartoes.Application.Abstractions;
using Cartoes.Domain.Entities;
using MongoDB.Driver;

namespace Cartoes.Infrastructure.Persistence;

internal sealed class EmissaoCartoesRepository(IMongoCollection<EmissaoCartoesDocumento> colecao) : IEmissaoCartoesRepository
{
    public Task<bool> ExisteParaPropostaAsync(Guid propostaId, CancellationToken cancellationToken) =>
        colecao.Find(d => d.PropostaId == propostaId).AnyAsync(cancellationToken);

    public async Task<bool> AdicionarAsync(EmissaoCartoes emissao, CancellationToken cancellationToken)
    {
        try
        {
            await colecao.InsertOneAsync(EmissaoCartoesDocumento.De(emissao), cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<EmissaoCartoes>> ListarPorClienteAsync(Guid clienteId, CancellationToken cancellationToken)
    {
        var documentos = await colecao
            .Find(d => d.ClienteId == clienteId)
            .SortBy(d => d.EmitidoEm)
            .ToListAsync(cancellationToken);

        return documentos.Select(d => d.ParaDominio()).ToList();
    }
}
