using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.UI.Matchups.Dtos;
using SportsData.Api.Application.UI.Matchups.Queries.GetMatchupPreview;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.UI.Matchups;

[ApiController]
[Route("ui/matchup")]
public class MatchupController : ApiControllerBase
{
    [HttpGet("{id}/preview")]
    [Authorize]
    public async Task<ActionResult<MatchupPreviewDto>> GetPreviewById(
        Guid id,
        [FromServices] IGetMatchupPreviewQueryHandler handler)
    {
        var query = new GetMatchupPreviewQuery { ContestId = id };
        var result = await handler.ExecuteAsync(query);
        return result.ToActionResult();
    }
}
