using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.Athletes.Queries.GetAthleteDetails;
using SportsData.Api.Application.Athletes.Queries.GetPickemAthletes;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.Athletes;

[ApiController]
[Route("api/{sport}/{league}/athletes")]
public class AthletesController : ApiControllerBase
{
    /// <summary>
    /// Roster-builder grid feed for Player Pick'em: active FBS athletes at
    /// a position with the week's opponent, opponent defensive allowance
    /// per game, and current/previous season stat blocks. Literal route
    /// segment — declared before the guid route it would otherwise shadow.
    /// </summary>
    [HttpGet("pickem")]
    public async Task<ActionResult<AthleteMatchupSummariesDto>> GetPickemAthletes(
        [FromRoute] string sport,
        [FromRoute] string league,
        [FromQuery] string position,
        [FromQuery] int seasonYear,
        [FromQuery] int week,
        [FromQuery] string? phase,
        [FromServices] IGetPickemAthletesQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(
            new GetPickemAthletesQuery(sport, league, position, seasonYear, week, phase ?? "regular"),
            cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Full athlete drill-down for the web athlete page: athlete record,
    /// every season, and per-season statistic documents. GUID route — the
    /// slug convention assumes uniqueness athlete slugs don't have.
    /// </summary>
    [HttpGet("{athleteId:guid}")]
    public async Task<ActionResult<AthleteDetailDto>> GetAthleteDetails(
        [FromRoute] string sport,
        [FromRoute] string league,
        [FromRoute] Guid athleteId,
        [FromServices] IGetAthleteDetailsQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(
            new GetAthleteDetailsQuery(sport, league, athleteId),
            cancellationToken);
        return result.ToActionResult();
    }
}
