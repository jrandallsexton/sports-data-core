using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.Models.Commands.CreateModelFamily;
using SportsData.Api.Application.Models.Queries.GetModelFamilies;
using SportsData.Api.Infrastructure.Auth;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.Models;

/// <summary>
/// Model family management for the admin Model Manager page: the lineage a
/// lab seat follows across versions (docs/features/model-family.md).
/// Every action is admin-only: [AdminApiToken] is on the class.
/// </summary>
[ApiController]
[Route("api/model-families")]
[AdminApiToken]
public class ModelFamiliesController : ApiControllerBase
{
    [HttpPost]
    public async Task<ActionResult<Guid>> CreateModelFamily(
        [FromBody] CreateModelFamilyCommand command,
        [FromServices] ICreateModelFamilyCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(command, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet]
    public async Task<ActionResult<List<ModelFamilyDto>>> GetModelFamilies(
        [FromServices] IGetModelFamiliesQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(cancellationToken);
        return result.ToActionResult();
    }
}
