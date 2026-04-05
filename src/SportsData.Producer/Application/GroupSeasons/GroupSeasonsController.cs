using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;
using SportsData.Producer.Application.GroupSeasons.Queries.GetConferenceIdsBySlugs;
using SportsData.Producer.Application.GroupSeasons.Queries.GetConferenceNamesAndSlugs;

namespace SportsData.Producer.Application.GroupSeasons;

[Route("api/group-seasons")]
[ApiController]
public class GroupSeasonsController : ControllerBase
{
    [HttpGet("conferences")]
    public async Task<ActionResult<List<ConferenceDivisionNameAndSlugDto>>> GetConferenceNamesAndSlugs(
        [FromQuery] int seasonYear,
        [FromServices] IGetConferenceNamesAndSlugsQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetConferenceNamesAndSlugsQuery(seasonYear);
        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }

    [HttpPost("conference-ids-by-slugs")]
    public async Task<ActionResult<Dictionary<Guid, string>>> GetConferenceIdsBySlugs(
        [FromBody] GetConferenceIdsBySlugsRequest request,
        [FromServices] IGetConferenceIdsBySlugsQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetConferenceIdsBySlugsQuery(request.SeasonYear, request.Slugs);
        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }
}
