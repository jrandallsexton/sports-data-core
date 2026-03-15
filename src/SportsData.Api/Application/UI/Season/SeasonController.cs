using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.UI.Season.Queries.GetSeasonOverview;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.UI.Season;

[ApiController]
[Route("ui/season")]
[Authorize]
public class SeasonController : ApiControllerBase
{
    [HttpGet("{seasonYear}/overview")]
    public async Task<ActionResult<SeasonOverviewDto>> GetSeasonOverview(
        [FromRoute] int seasonYear,
        [FromServices] IGetSeasonOverviewQueryHandler handler,
        CancellationToken cancellationToken)
    {
        // TODO: Support multiple sports
        var query = new GetSeasonOverviewQuery { SeasonYear = seasonYear, Sport = Sport.FootballNcaa };
        var result = await handler.ExecuteAsync(query, cancellationToken);
        return result.ToActionResult();
    }
}
