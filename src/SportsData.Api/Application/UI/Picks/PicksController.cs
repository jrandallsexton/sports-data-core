using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.UI.Picks.Commands.SubmitPick;
using SportsData.Api.Application.UI.Picks.Dtos;
using SportsData.Api.Application.UI.Picks.Queries.GetPickAccuracyByWeek;
using SportsData.Api.Application.UI.Picks.Queries.GetPickRecordWidget;
using SportsData.Api.Application.UI.Picks.Queries.GetUserPicksByGroupAndWeek;
using SportsData.Api.Extensions;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

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
    public async Task<ActionResult<List<UserPickDto>>> GetUserPicksByGroupAndWeek(
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
