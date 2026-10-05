using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.Picks.Commands.BackfillUserPickBetPoints;
using SportsData.Api.Infrastructure.Auth;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.Picks;

/// <summary>
/// Operator tools for user picks. Every action is admin-only: [AdminApiToken]
/// is on the class. The user-facing picks surface is
/// <c>UI/Picks/PicksController</c> at <c>ui/picks</c>.
/// </summary>
[ApiController]
[Route("api/picks")]
[AdminApiToken]
public class PicksController : ApiControllerBase
{
    /// <summary>
    /// Computes the simulated $1 bet columns (PointsSU, PointsATS, PointsOU)
    /// on every already-scored UserPick from its contest's finalized result
    /// and its league matchup's prices: enqueues one background job per
    /// distinct contest. IsCorrect and PointsAwarded are not touched.
    /// Returns 202 with the correlation id and per-sport job counts.
    /// Idempotent, safe to re-run.
    /// Example: POST /api/picks/bet-points/backfill
    /// </summary>
    [HttpPost("bet-points/backfill")]
    public async Task<ActionResult<BackfillUserPickBetPointsResult>> BackfillUserPickBetPoints(
        [FromServices] IBackfillUserPickBetPointsCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(
            new BackfillUserPickBetPointsCommand(),
            cancellationToken);
        return result.ToActionResult();
    }
}
