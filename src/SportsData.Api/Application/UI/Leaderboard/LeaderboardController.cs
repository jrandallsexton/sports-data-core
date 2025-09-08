using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.UI.Leaderboard.Dtos;
using SportsData.Api.Extensions;

namespace SportsData.Api.Application.UI.Leaderboard;

[ApiController]
[Route("ui/leaderboard")]
[Authorize]
public class LeaderboardController : ControllerBase
{
    private readonly ILeaderboardService _leaderboardService;

    public LeaderboardController(ILeaderboardService leaderboardService)
    {
        _leaderboardService = leaderboardService;
    }

    [HttpGet("{groupId}")]
    [Authorize]
    public async Task<ActionResult<List<LeaderboardUserDto>>> GetLeaderboard(
        [FromRoute] Guid groupId,
        CancellationToken cancellationToken)
    {
        var leaderboard = await _leaderboardService
            .GetLeaderboardAsync(groupId, cancellationToken);

        return Ok(leaderboard);
    }

    [HttpGet("widget")]
    [Authorize]
    public async Task<ActionResult<LeaderboardWidgetDto>> GetLeaderboardWidget(
        CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetCurrentUserId();

        var leaderboardWidget = await _leaderboardService
            .GetLeaderboardWidgetForUser(userId, 2025, cancellationToken);

        return Ok(leaderboardWidget);
    }
}