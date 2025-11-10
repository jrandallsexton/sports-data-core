using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.UI.Picks.Dtos;
using SportsData.Api.Application.UI.Picks.PicksPage;
using SportsData.Api.Extensions;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.UI.Picks;

[ApiController]
[Route("ui/picks")]
public class PicksController : ApiControllerBase
{
    private readonly IPickService _userPickService;

    public PicksController(IPickService userPickService)
    {
        _userPickService = userPickService;
    }

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
        CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetCurrentUserId();

        var result = await _userPickService.SubmitPickAsync(userId, request, cancellationToken);

        if (result.IsSuccess)
            return NoContent();

        return result.ToActionResult();
    }

    [HttpGet("{groupId}/week/{week}")]
    public async Task<ActionResult<List<UserPickDto>>> GetUserPicksByGroupAndWeek(
        Guid groupId,
        int week,
        CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetCurrentUserId();

        var result = await _userPickService.GetUserPicksByGroupAndWeek(userId, groupId, week, cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("{season}/widget")]
    [Authorize]
    public async Task<ActionResult<PickRecordWidgetDto>> GetPickRecordWidget([FromRoute] int season,
        CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetCurrentUserId();

        var result = await _userPickService.GetPickRecordWidget(userId, season, cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("{season}/widget/synthetic")]
    [Authorize]
    public async Task<ActionResult<PickRecordWidgetDto>> GetPickRecordWidgetForSynthetic([FromRoute] int season,
        CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetCurrentUserId();

        var result = await _userPickService.GetPickRecordWidgetForSynthetic(userId, season, cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("chart")]
    [Authorize]
    public async Task<ActionResult<List<PickAccuracyByWeekDto>>> GetPickAccuracyChart(
        CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetCurrentUserId();

        var result = await _userPickService.GetPickAccuracyByWeek(userId, cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("chart/synthetic")]
    [Authorize]
    public async Task<ActionResult<PickAccuracyByWeekDto>> GetPickAccuracyChartForSynthetic(
        CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetCurrentUserId();

        var result = await _userPickService.GetPickAccuracyByWeekForSynthetic(userId, cancellationToken);

        return result.ToActionResult();
    }
}