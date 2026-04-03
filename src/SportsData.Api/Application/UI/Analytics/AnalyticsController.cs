using Microsoft.AspNetCore.Mvc;

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
}
