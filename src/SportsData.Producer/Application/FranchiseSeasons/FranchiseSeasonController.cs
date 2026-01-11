using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;
using SportsData.Producer.Application.FranchiseSeasons.Commands.EnqueueFranchiseSeasonMetricsGeneration;
using SportsData.Producer.Application.FranchiseSeasons.Queries.GetFranchiseSeasonMetricsById;
using SportsData.Producer.Application.FranchiseSeasons.Queries.GetFranchiseSeasonMetricsBySeasonYear;

namespace SportsData.Producer.Application.FranchiseSeasons;

[Route("api/franchise-seasons")]
[ApiController]
public class FranchiseSeasonController : ControllerBase
{
    [HttpGet("id/{franchiseSeasonId}/metrics")]
    public async Task<ActionResult<FranchiseSeasonMetricsDto>> GetFranchiseSeasonMetricsByFranchiseSeasonId(
        [FromRoute] Guid franchiseSeasonId,
        [FromServices] IGetFranchiseSeasonMetricsByIdQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetFranchiseSeasonMetricsByIdQuery(franchiseSeasonId);
        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("seasonYear/{seasonYear}/metrics")]
    public async Task<ActionResult<List<FranchiseSeasonMetricsDto>>> GetFranchiseSeasonMetricsBySeasonYear(
        [FromRoute] int seasonYear,
        [FromServices] IGetFranchiseSeasonMetricsBySeasonYearQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetFranchiseSeasonMetricsBySeasonYearQuery(seasonYear);
        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }

    [HttpPost("seasonYear/{seasonYear}/metrics/generate")]
    public async Task<ActionResult<Guid>> GenerateFranchiseSeasonMetrics(
        [FromRoute] int seasonYear,
        [FromServices] IEnqueueFranchiseSeasonMetricsGenerationCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new EnqueueFranchiseSeasonMetricsGenerationCommand(
            seasonYear,
            Core.Common.Sport.FootballNcaa); // TODO: remove hard-coding

        var result = await handler.ExecuteAsync(command, cancellationToken);

        return result.ToActionResult();
    }
}
