using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.Admin;
using SportsData.Api.Application.Models.Queries.GetModelLabMatrix;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.Models;

/// <summary>
/// Model Consensus Lab (admin Model Lab page). Every action is admin-only:
/// [AdminApiToken] is on the class (accepts the shared header or a Firebase
/// JWT with the Admin role).
/// </summary>
[ApiController]
[Route("api/model-lab")]
[AdminApiToken]
public class ModelLabController : ApiControllerBase
{
    /// <summary>
    /// Model Consensus Lab week matrix: every contest any pick'em league
    /// carries for (sport, season, week) x every active lab-reachable
    /// model, with each pair's latest experiment picks. Missing cells
    /// are generated one at a time via the experiment endpoint's
    /// modelId parameter. See docs/features/model-consensus-lab.md.
    /// </summary>
    [HttpGet("matrix")]
    public async Task<ActionResult<ModelLabMatrixDto>> GetModelLabMatrix(
        [FromServices] IGetModelLabMatrixQueryHandler handler,
        [FromQuery] Sport sport = Sport.FootballNcaa,
        [FromQuery] int seasonYear = 0,
        [FromQuery] int week = 0,
        [FromQuery] Guid? promptId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.ExecuteAsync(
            new GetModelLabMatrixQuery
            {
                Sport = sport,
                SeasonYear = seasonYear,
                Week = week,
                PromptId = promptId
            },
            cancellationToken);

        return result.ToActionResult();
    }
}
