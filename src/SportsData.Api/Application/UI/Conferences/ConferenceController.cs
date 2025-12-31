using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.UI.Conferences.Dtos;
using SportsData.Api.Application.UI.Conferences.Queries.GetConferenceNamesAndSlugs;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.UI.Conferences;

[ApiController]
[Route("ui/conferences")]
public class ConferenceController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ConferenceNameAndSlugDto>>> GetConferenceNamesAndSlugs(
        [FromServices] IGetConferenceNamesAndSlugsQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetConferenceNamesAndSlugsQuery();
        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }
}
