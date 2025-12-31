using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.UI.Map.Dtos;
using SportsData.Api.Application.UI.Map.Queries.GetMapMatchups;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.UI.Map;

[ApiController]
[Route("ui/map")]
public class MapController : ApiControllerBase
{
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<GetMapMatchupsResponse>> GetMatchups(
        [FromQuery] Guid? leagueId,
        [FromQuery] int? weekNumber,
        [FromServices] IGetMapMatchupsQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetMapMatchupsQuery
        {
            LeagueId = leagueId,
            WeekNumber = weekNumber
        };

        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }
}
