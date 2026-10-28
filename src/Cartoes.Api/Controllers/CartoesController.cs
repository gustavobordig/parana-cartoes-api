using Asp.Versioning;
using Cartoes.Application.Cartoes;
using Cartoes.Application.Cartoes.Listar;
using Microsoft.AspNetCore.Mvc;

namespace Cartoes.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/cartoes")]
[Produces("application/json")]
public sealed class CartoesController(ListarCartoesHandler listarCartoes) : ControllerBase
{
    [HttpGet("{clienteId:guid}")]
    [EndpointSummary("Lista os cartões emitidos para um cliente.")]
    [EndpointDescription("A emissão é assíncrona: lista vazia significa proposta negada ou ainda em processamento. O número sai mascarado, só com os 4 últimos dígitos.")]
    [ProducesResponseType<IReadOnlyList<CartaoResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarPorClienteId(Guid clienteId, CancellationToken cancellationToken) =>
        Ok(await listarCartoes.HandleAsync(clienteId, cancellationToken));
}
