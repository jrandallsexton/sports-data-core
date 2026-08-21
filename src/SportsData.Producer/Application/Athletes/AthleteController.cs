using Microsoft.AspNetCore.Mvc;

using SportsData.Core.DependencyInjection;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Athletes.Commands.RequestAthleteSeasonStatisticsSourcing;
using SportsData.Producer.Application.Athletes.Queries.GetAthleteById;

namespace SportsData.Producer.Application.Athletes;

/// <summary>
/// Body for the season-statistics sourcing endpoint. SeasonType scopes the
/// synthesized statistics URL: 2 = regular season, 3 = through postseason
/// (default — full-season totals).
/// </summary>
public class AthleteSeasonStatisticsSourcingRequest
{
    public int SeasonType { get; set; } = 3;
}

[Route("api/athletes")]
[ApiController]
public class AthleteController : ControllerBase
{
    /// <summary>
    /// Full athlete drill-down: athlete record + every season + statistic
    /// documents. GUID-keyed — athlete slugs are not unique (~15% collide),
    /// so slug routing is not an option here.
    /// </summary>
    [HttpGet("{athleteId:guid}")]
    public async Task<ActionResult<AthleteDetailDto>> GetAthleteById(
        [FromRoute] Guid athleteId,
        [FromServices] IGetAthleteByIdQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(new GetAthleteByIdQuery(athleteId), cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Fans out season-type-scoped statistics DocumentRequests for every
    /// ACTIVE athlete rostered in the season year (immediate driver: the
    /// 2025 NCAAFB season-stats backfill). ~25k athletes per season, so it
    /// runs as a background job; the returned correlation id is the Seq
    /// handle for the whole batch, same contract as the franchise-season
    /// sourcing endpoint.
    /// </summary>
    [HttpPost("seasonYear/{seasonYear}/statistics/source")]
    public ActionResult<Guid> RequestAthleteSeasonStatisticsSourcing(
        [FromRoute] int seasonYear,
        [FromBody] AthleteSeasonStatisticsSourcingRequest request,
        [FromServices] IProvideBackgroundJobs backgroundJobProvider,
        [FromServices] IAppMode appMode)
    {
        var correlationId = Guid.NewGuid();

        var command = new RequestAthleteSeasonStatisticsSourcingCommand(
            seasonYear,
            appMode.CurrentSport,
            request.SeasonType,
            correlationId);

        backgroundJobProvider.Enqueue<IRequestAthleteSeasonStatisticsSourcingCommandHandler>(
            h => h.ExecuteAsync(command, CancellationToken.None));

        return Accepted(correlationId);
    }
}
