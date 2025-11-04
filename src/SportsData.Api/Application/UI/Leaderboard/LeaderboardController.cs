using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.UI.Leaderboard.Dtos;
using SportsData.Api.Extensions;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.UI.Leaderboard;

[ApiController]
[Route("ui/leaderboard")]
[Authorize]
public class LeaderboardController : ApiControllerBase
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
        var result = await _leaderboardService
            .GetLeaderboardAsync(groupId, cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("widget")]
    [Authorize]
    public async Task<ActionResult<LeaderboardWidgetDto>> GetLeaderboardWidget(
        CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetCurrentUserId();

        var result = await _leaderboardService
            .GetLeaderboardWidgetForUser(userId, 2025, cancellationToken);

        return result.ToActionResult();
    }
}