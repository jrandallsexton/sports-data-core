using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.Internal.Queries.GetContestIdsInLeagues;
using SportsData.Core.Common;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Api;

namespace SportsData.Api.Application.Internal;

/// <summary>
/// Service-to-service inquiries about league usage. Consumed by Producer's
/// CompetitionStreamScheduler so live-sourcing covers ONLY games that back
/// a pick'em league (team or player — both generate PickemGroupMatchup
/// rows) instead of every game ESPN publishes.
///
/// Anonymous by design: the answer ("this contest appears in some league")
/// carries no user data and no league identity, and Producer reaches this
/// over in-cluster DNS. Revisit with a service credential if the surface
/// ever grows beyond membership-free facts.
/// </summary>
[ApiController]
[Route("system/league-contests")]
public class LeagueContestsController : ApiControllerBase
{
    [HttpPost("in-use")]
    public async Task<ActionResult<List<Guid>>> GetContestIdsInLeagues(
        [FromBody] GetContestIdsInLeaguesRequest request,
        [FromServices] IGetContestIdsInLeaguesQueryHandler handler,
        CancellationToken cancellationToken)
    {
        if (request?.ContestIds is null)
        {
            return BadRequest("contestIds is required.");
        }

        // Bound the probe so a malformed caller can't turn this into a
        // giant ANY() scan. Producer batches per season week (< 1k).
        if (request.ContestIds.Length > 5000)
        {
            return BadRequest("Too many contest ids; batch the request.");
        }

        var result = await handler.ExecuteAsync(
            new GetContestIdsInLeaguesQuery(request.ContestIds), cancellationToken);
        return result.ToActionResult();
    }
}
