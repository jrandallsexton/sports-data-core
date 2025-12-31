using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.UI.Analytics.Queries.GetFranchiseSeasonMetrics;
using SportsData.Core.Common;
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
        [FromServices] IGetFranchiseSeasonMetricsQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetFranchiseSeasonMetricsQuery { SeasonYear = seasonYear };
        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }
}
