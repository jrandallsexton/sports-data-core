using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.Seasons.Queries.GetCurrentSeason;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.Seasons;

[ApiController]
[Route("api/{sport}/{league}/seasons")]
public class SeasonsController : ApiControllerBase
{
    /// <summary>
    /// The current-or-upcoming season for the sport, with its phases. Standard
    /// endpoint returning raw phase data (TypeCode + dates); clients interpret it
    /// (e.g. the home off-season countdown derives kickoff from the Regular
    /// Season phase's StartDate).
    /// </summary>
    [HttpGet("current", Name = "GetCurrentSeason")]
    [ProducesResponseType(typeof(CurrentSeasonDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CurrentSeasonDto>> GetCurrentSeason(
        [FromServices] IGetCurrentSeasonQueryHandler handler,
        [FromRoute] string sport,
        [FromRoute] string league,
        CancellationToken cancellationToken = default)
    {
        var query = new GetCurrentSeasonQuery
        {
            Sport = sport,
            League = league
        };

        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }
}
