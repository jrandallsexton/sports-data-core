using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.MiniGames.Commands.SubmitMiniGameResults;
using SportsData.Api.Application.MiniGames.Queries.GetMiniGameStats;
using SportsData.Api.Extensions;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.MiniGames;

/// <summary>
/// The signed-in user's mini-game results (the football card game today).
/// Private to the user; see docs/features/mini-game-results.md.
/// </summary>
[ApiController]
[Route("user/me/minigames")]
[Authorize]
public class MiniGamesController : ApiControllerBase
{
    [HttpPost("results")]
    public async Task<ActionResult<SubmitMiniGameResultsResponse>> SubmitResults(
        [FromBody] SubmitMiniGameResultsCommand command,
        [FromServices] ISubmitMiniGameResultsCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetCurrentUserId();

        var result = await handler.ExecuteAsync(userId, command, cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("{gameKey}/stats")]
    public async Task<ActionResult<MiniGameStatsDto>> GetStats(
        string gameKey,
        [FromServices] IGetMiniGameStatsQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetMiniGameStatsQuery
        {
            UserId = HttpContext.GetCurrentUserId(),
            GameKey = gameKey
        };

        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }
}
