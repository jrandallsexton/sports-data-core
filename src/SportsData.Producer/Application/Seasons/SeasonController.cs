using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;
using SportsData.Producer.Application.Seasons.Queries.GetSeasonOverview;

namespace SportsData.Producer.Application.Seasons;

[Route("api/seasons")]
[ApiController]
public class SeasonController : ControllerBase
{
    [HttpGet("{seasonYear}/overview")]
    public async Task<ActionResult<SeasonOverviewDto>> GetSeasonOverview(
        [FromRoute] int seasonYear,
        [FromServices] IGetSeasonOverviewQueryHandler handler,
        CancellationToken cancellationToken = default)
    {
        var query = new GetSeasonOverviewQuery(seasonYear);
        var result = await handler.ExecuteAsync(query, cancellationToken);
        return result.ToActionResult();
    }
}
