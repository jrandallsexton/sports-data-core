using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.Contests.Queries.GetContestById;
using SportsData.Api.Application.Contests.Queries.GetContestById.Dtos;
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
}
