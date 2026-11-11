using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.UI.Analytics.Queries.GetAthleteSeasonLeaders;
using SportsData.Api.Application.UI.Analytics.Queries.GetFranchiseSeasonMetrics;
using SportsData.Core.Common;
using SportsData.Core.Common.Mapping;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.UI.Analytics;

[ApiController]
[Route("ui/analytics")]
public class AnalyticsController : ApiControllerBase
{
    [HttpGet("franchise-season/{seasonYear}")]
    public async Task<ActionResult<List<FranchiseSeasonMetricsDto>>> GetFranchiseSeasonMetrics(
        [FromRoute] int seasonYear,
        [FromQuery] string sport = "football",
        [FromQuery] string league = "ncaa",
        [FromServices] IGetFranchiseSeasonMetricsQueryHandler handler = default!,
        CancellationToken cancellationToken = default)
    {
        var mode = ModeMapper.ResolveMode(sport, league);
        var query = new GetFranchiseSeasonMetricsQuery { SeasonYear = seasonYear, Sport = mode };
        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }

    /// <summary>
    /// Season leaderboard for one athlete stat. Category and stat are ESPN
    /// names (e.g. passing / passingYards).
    /// </summary>
    [HttpGet("leaders/{seasonYear}")]
    public async Task<ActionResult<AthleteSeasonLeadersDto>> GetAthleteSeasonLeaders(
        [FromRoute] int seasonYear,
        [FromQuery] string category,
        [FromQuery] string stat,
        [FromQuery] bool perGame = false,
        [FromQuery] int limit = 10,
        [FromQuery] string sport = "football",
        [FromQuery] string league = "ncaa",
        [FromServices] IGetAthleteSeasonLeadersQueryHandler handler = default!,
        CancellationToken cancellationToken = default)
    {
        var mode = ModeMapper.ResolveMode(sport, league);
        var query = new GetAthleteSeasonLeadersQuery
        {
            SeasonYear = seasonYear,
            Sport = mode,
            Category = category,
            Stat = stat,
            PerGame = perGame,
            Limit = limit,
        };
        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }
}
