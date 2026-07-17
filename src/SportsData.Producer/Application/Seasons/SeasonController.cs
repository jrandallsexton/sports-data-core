using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;
using SportsData.Producer.Application.Seasons.Queries.GetCompletedSeasonWeeks;
using SportsData.Producer.Application.Seasons.Queries.GetCurrentAndLastSeasonWeeks;
using SportsData.Producer.Application.Seasons.Queries.GetCurrentSeason;
using SportsData.Producer.Application.Seasons.Queries.GetCurrentSeasonWeek;
using SportsData.Producer.Application.Seasons.Queries.GetSeasonOverview;
using SportsData.Producer.Application.Seasons.Queries.GetSeasonWeeksByDateRange;

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

    /// <summary>
    /// The current-or-upcoming season with its phases, for this Producer's sport.
    /// Backs the API's per-sport <c>seasons/current</c> resource (which the
    /// off-season kickoff countdown consumes). Returns raw phase data — no
    /// countdown computation here.
    /// </summary>
    [HttpGet("current")]
    public async Task<ActionResult<CurrentSeasonDto>> GetCurrentSeason(
        [FromServices] IGetCurrentSeasonQueryHandler handler,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.ExecuteAsync(new GetCurrentSeasonQuery(), cancellationToken);
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

    /// <summary>
    /// Resolves the SeasonWeek(s) overlapping the requested date range.
    /// Drives the API-side league-creation handler for windowed leagues
    /// (single-day, multi-day, multi-week), replacing the prior "always
    /// use the current week" model that produced orphan empty
    /// <c>PickemGroupWeek</c> rows.
    /// </summary>
    [HttpGet("weeks/by-date-range")]
    public async Task<ActionResult<List<CanonicalSeasonWeekDto>>> GetSeasonWeeksByDateRange(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromServices] IGetSeasonWeeksByDateRangeQueryHandler handler,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.ExecuteAsync(
            new GetSeasonWeeksByDateRangeQuery(from, to),
            cancellationToken);
        return result.ToActionResult();
    }
}
