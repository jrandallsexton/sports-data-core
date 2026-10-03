using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.UI.Picks.Advisor.Dtos;
using SportsData.Api.Application.UI.Picks.Advisor.Planner;
using SportsData.Api.Application.UI.Picks.Advisor.Queries.GetPickAdvice;
using SportsData.Api.Application.UI.Picks.Commands.SubmitPick;
using SportsData.Api.Application.UI.Picks.Dtos;
using SportsData.Api.Application.UI.Picks.Queries.GetPickAccuracyByWeek;
using SportsData.Api.Application.UI.Picks.Queries.GetPickRecordWidget;
using SportsData.Api.Application.UI.Picks.Queries.GetUserPicksByGroupAndWeek;
using SportsData.Api.Extensions;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

using SportsData.Api.Application.Common.Enums;

namespace SportsData.Api.Application.UI.Picks;

[ApiController]
[Route("ui/picks")]
public class PicksController : ApiControllerBase
{
    [HttpGet("{sport}/{season}/{week}")]
    [Authorize]
    public async Task<IActionResult> GetPicksForWeek(
        [FromRoute] Sport sport,
        [FromRoute] int season,
        [FromRoute] int week)
    {
        var userId = HttpContext.GetCurrentUserId();
        await Task.Delay(100);
        return Ok();
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<Guid>> SubmitPick(
        [FromBody] SubmitUserPickRequest request,
        [FromServices] ISubmitPickCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetCurrentUserId();

        var command = new SubmitPickCommand
        {
            UserId = userId,
            PickemGroupId = request.PickemGroupId,
            ContestId = request.ContestId,
            Week = request.Week,
            PickType = request.PickType,
            FranchiseSeasonId = request.FranchiseSeasonId,
            OverUnder = request.OverUnder,
            ConfidencePoints = request.ConfidencePoints,
            TiebreakerGuessTotal = request.TiebreakerGuessTotal,
            TiebreakerGuessHome = request.TiebreakerGuessHome,
            TiebreakerGuessAway = request.TiebreakerGuessAway
        };

        var result = await handler.ExecuteAsync(command, cancellationToken);

        if (result.IsSuccess)
            return NoContent();

        return result.ToActionResult();
    }

    [HttpGet("{groupId}/week/{week}")]
    [Authorize]
    public async Task<ActionResult<UserPicksResultDto>> GetUserPicksByGroupAndWeek(
        Guid groupId,
        int week,
        [FromServices] IGetUserPicksByGroupAndWeekQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetCurrentUserId();

        var query = new GetUserPicksByGroupAndWeekQuery
        {
            UserId = userId,
            GroupId = groupId,
            WeekNumber = week
        };

        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }

    /// <summary>
    /// StatBot's advice for the caller in this league-week: standings analysis,
    /// recommended risk level, and a full suggested sheet for <paramref name="level"/>
    /// (the recommendation when omitted). Read-only; the client applies picks
    /// through the normal submit path. See docs/features/statbot-advisor.md.
    /// </summary>
    [HttpGet("{groupId}/week/{week}/advice")]
    [Authorize]
    public async Task<ActionResult<PickAdviceDto>> GetPickAdvice(
        Guid groupId,
        int week,
        [FromQuery] AdvisorLevel? level,
        [FromServices] IGetPickAdviceQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetPickAdviceQuery
        {
            UserId = HttpContext.GetCurrentUserId(),
            LeagueId = groupId,
            Week = week,
            Level = level
        };

        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }

    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]
    [HttpGet("{season}/widget")]
    [Authorize]
    public async Task<ActionResult<PickRecordWidgetDto>> GetPickRecordWidget(
        [FromRoute] int season,
        [FromServices] IGetPickRecordWidgetQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetCurrentUserId();

        var query = new GetPickRecordWidgetQuery
        {
            UserId = userId,
            SeasonYear = season,
            ForSynthetic = false
        };

        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }

    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]
    [HttpGet("{season}/widget/synthetic")]
    [Authorize]
    public async Task<ActionResult<PickRecordWidgetDto>> GetPickRecordWidgetForSynthetic(
        [FromRoute] int season,
        [FromServices] IGetPickRecordWidgetQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetCurrentUserId();

        var query = new GetPickRecordWidgetQuery
        {
            UserId = userId,
            SeasonYear = season,
            ForSynthetic = true
        };

        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }

    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]
    [HttpGet("chart")]
    [Authorize]
    public async Task<ActionResult<List<PickAccuracyByWeekDto>>> GetPickAccuracyChart(
        [FromServices] IGetPickAccuracyByWeekQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetCurrentUserId();

        var query = new GetPickAccuracyByWeekQuery
        {
            UserId = userId,
            ForSynthetic = false
        };

        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }

    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]
    [HttpGet("chart/synthetic")]
    [Authorize]
    public async Task<ActionResult<PickAccuracyByWeekDto>> GetPickAccuracyChartForSynthetic(
        [FromServices] IGetPickAccuracyByWeekQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetCurrentUserId();

        var query = new GetPickAccuracyByWeekQuery
        {
            UserId = userId,
            ForSynthetic = true
        };

        var result = await handler.ExecuteForSyntheticAsync(query, cancellationToken);

        return result.ToActionResult();
    }
}
