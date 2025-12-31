using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.UI.Leaderboard.Dtos;
using SportsData.Api.Application.UI.Leaderboard.Queries.GetLeaderboard;
using SportsData.Api.Application.UI.Leaderboard.Queries.GetLeaderboardWidget;
using SportsData.Api.Extensions;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.UI.Leaderboard;

[ApiController]
[Route("ui/leaderboard")]
[Authorize]
public class LeaderboardController : ApiControllerBase
{
    [HttpGet("{groupId}")]
    [Authorize]
    public async Task<ActionResult<List<LeaderboardUserDto>>> GetLeaderboard(
        [FromRoute] Guid groupId,
        [FromServices] IGetLeaderboardQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetLeaderboardQuery { GroupId = groupId };
        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }

    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]
    [HttpGet("widget")]
    [Authorize]
    public async Task<ActionResult<LeaderboardWidgetDto>> GetLeaderboardWidget(
        [FromServices] IGetLeaderboardWidgetQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetCurrentUserId();

        var query = new GetLeaderboardWidgetQuery { UserId = userId };
        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }
}
