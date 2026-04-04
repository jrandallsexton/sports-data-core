using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;
using SportsData.Producer.Application.Seasons.Queries.GetCompletedSeasonWeeks;
using SportsData.Producer.Application.Seasons.Queries.GetCurrentAndLastSeasonWeeks;
using SportsData.Producer.Application.Seasons.Queries.GetCurrentSeasonWeek;
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

    [HttpGet("current-week")]
    public async Task<ActionResult<CanonicalSeasonWeekDto>> GetCurrentSeasonWeek(
        [FromServices] IGetCurrentSeasonWeekQueryHandler handler,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.ExecuteAsync(new GetCurrentSeasonWeekQuery(), cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("current-and-last-weeks")]
    public async Task<ActionResult<List<CanonicalSeasonWeekDto>>> GetCurrentAndLastSeasonWeeks(
        [FromServices] IGetCurrentAndLastSeasonWeeksQueryHandler handler,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.ExecuteAsync(new GetCurrentAndLastSeasonWeeksQuery(), cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{seasonYear}/completed-weeks")]
    public async Task<ActionResult<List<CanonicalSeasonWeekDto>>> GetCompletedSeasonWeeks(
        [FromRoute] int seasonYear,
        [FromServices] IGetCompletedSeasonWeeksQueryHandler handler,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.ExecuteAsync(new GetCompletedSeasonWeeksQuery(seasonYear), cancellationToken);
        return result.ToActionResult();
    }
}
