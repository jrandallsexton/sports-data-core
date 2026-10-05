using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.Models.Commands.CreateModelProvider;
using SportsData.Api.Application.Models.Queries.GetModelProviders;
using SportsData.Api.Infrastructure.Auth;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.Models;

/// <summary>
/// LLM provider management for the admin Model Manager page. Every action is
/// admin-only: [AdminApiToken] is on the class (accepts the shared header or
/// a Firebase JWT with the Admin role).
/// </summary>
[ApiController]
[Route("api/model-providers")]
[AdminApiToken]
public class ModelProvidersController : ApiControllerBase
{
    /// <summary>
    /// Create an LLM provider row (pairs with a code-side client via
    /// Kind — credentials live in AppConfig, never the DB).
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Guid>> CreateModelProvider(
        [FromBody] CreateModelProviderCommand command,
        [FromServices] ICreateModelProviderCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(command, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet]
    public async Task<ActionResult<List<ModelProviderDto>>> GetModelProviders(
        [FromServices] IGetModelProvidersQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(cancellationToken);
        return result.ToActionResult();
    }
}
