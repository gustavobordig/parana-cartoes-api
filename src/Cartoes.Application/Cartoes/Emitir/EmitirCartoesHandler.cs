using Cartoes.Application.Abstractions;
using Cartoes.Domain.Entities;
using Microsoft.Extensions.Logging;
using ParanaBanco.Credito.Contracts;

namespace Cartoes.Application.Cartoes.Emitir;

public sealed class EmitirCartoesHandler(
    IEmissaoCartoesRepository repositorio,
    TimeProvider relogio,
    Random aleatorio,
    ILogger<EmitirCartoesHandler> logger)
{
    public const string StatusAprovada = "Aprovada";

    public async Task HandleAsync(PropostaGerada evento, CancellationToken cancellationToken)
    {
        if (evento.Status != StatusAprovada || evento.QuantidadeCartoes == 0)
        {
            logger.LogInformation("Proposta {PropostaId} com status {Status}; nenhum cartão emitido", evento.PropostaId, evento.Status);
            return;
        }

        if (await repositorio.ExisteParaPropostaAsync(evento.PropostaId, cancellationToken))
        {
            logger.LogInformation("Cartões da proposta {PropostaId} já emitidos; evento ignorado", evento.PropostaId);
            return;
        }

        var emissao = EmissaoCartoes.Emitir(
            evento.PropostaId,
            evento.ClienteId,
            evento.QuantidadeCartoes,
            evento.LimitePorCartao,
            relogio.GetUtcNow(),
            aleatorio);

        if (!await repositorio.AdicionarAsync(emissao, cancellationToken))
        {
            logger.LogInformation("Cartões da proposta {PropostaId} emitidos em paralelo; evento ignorado", evento.PropostaId);
            return;
        }

        logger.LogInformation(
            "{QuantidadeCartoes} cartão(ões) emitido(s) para o cliente {ClienteId}, limite total {LimiteTotal}",
            emissao.Cartoes.Count, emissao.ClienteId, emissao.LimiteTotal);
    }
}
