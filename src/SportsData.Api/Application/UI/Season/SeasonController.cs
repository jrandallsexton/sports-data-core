using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.UI.Season.Queries.GetSeasonOverview;
using SportsData.Core.Common;
using SportsData.Core.Common.Mapping;
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
        [FromQuery] string sport = "football",
        [FromQuery] string league = "ncaa",
        [FromServices] IGetSeasonOverviewQueryHandler handler = default!,
        CancellationToken cancellationToken = default)
    {
        var mode = ModeMapper.ResolveMode(sport, league);
        var query = new GetSeasonOverviewQuery { SeasonYear = seasonYear, Sport = mode };
        var result = await handler.ExecuteAsync(query, cancellationToken);
        return result.ToActionResult();
    }
}
