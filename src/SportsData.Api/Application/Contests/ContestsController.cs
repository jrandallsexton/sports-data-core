using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.Contests.Queries.GetContestById;
using SportsData.Api.Application.Contests.Queries.GetContestById.Dtos;
using SportsData.Api.Application.Contests.Queries.GetContestHistory;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.Contests;

[Route("api/{sport}/{league}/contests")]
[ApiController]
public class ContestsController : ControllerBase
{
    [HttpGet("{contestId:guid}")]
    [ProducesResponseType(typeof(ContestDetailResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ContestDetailResponseDto>> GetContestById(
        [FromServices] IGetContestByIdQueryHandler handler,
        [FromRoute] string sport,
        [FromRoute] string league,
        [FromRoute] Guid contestId,
        CancellationToken cancellationToken = default)
    {
        var query = new GetContestByIdQuery(sport, league, contestId);
        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }

    /// <summary>
    /// Historical context for a matchup: last N head-to-head meetings and
    /// each team's late-prior-season form — the same blocks the
    /// preview/insight models consume.
    /// </summary>
    [HttpGet("{contestId:guid}/history")]
    [ProducesResponseType(typeof(ContestPreviewHistoryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ContestPreviewHistoryDto>> GetContestHistory(
        [FromServices] IGetContestHistoryQueryHandler handler,
        [FromRoute] string sport,
        [FromRoute] string league,
        [FromRoute] Guid contestId,
        CancellationToken cancellationToken = default)
    {
        var query = new GetContestHistoryQuery(sport, league, contestId);
        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }
}
