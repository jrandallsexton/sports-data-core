using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.UI.Contest.Commands.RefreshContest;
using SportsData.Api.Application.UI.Contest.Commands.RefreshContestMedia;
using SportsData.Api.Application.UI.Contest.Queries.GetContestOverview;
using SportsData.Core.Common;
using SportsData.Core.Common.Mapping;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.UI.Contest;

[ApiController]
[Route("ui/contest")]
[Authorize]
public class ContestController : ApiControllerBase
{
    [HttpGet("{id}/overview")]
    public async Task<ActionResult<ContestOverviewDto>> GetContestById(
        [FromRoute] Guid id,
        [FromQuery] string sport = "football",
        [FromQuery] string league = "ncaa",
        [FromServices] IGetContestOverviewQueryHandler handler = default!,
        CancellationToken cancellationToken = default)
    {
        var mode = ModeMapper.ResolveMode(sport, league);
        var query = new GetContestOverviewQuery { ContestId = id, Sport = mode };
        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }

    [HttpPost("{id}/refresh")]
    public async Task<ActionResult<Guid>> RefreshContestById(
        [FromRoute] Guid id,
        [FromQuery] string sport = "football",
        [FromQuery] string league = "ncaa",
        [FromServices] IRefreshContestCommandHandler handler = default!,
        CancellationToken cancellationToken = default)
    {
        var mode = ModeMapper.ResolveMode(sport, league);
        var command = new RefreshContestCommand { ContestId = id, Sport = mode };
        var result = await handler.ExecuteAsync(command, cancellationToken);

        return result.ToActionResult();
    }

    [HttpPost("{id}/media/refresh")]
    public async Task<ActionResult<Guid>> RefreshContestMediaById(
        [FromRoute] Guid id,
        [FromQuery] string sport = "football",
        [FromQuery] string league = "ncaa",
        [FromServices] IRefreshContestMediaCommandHandler handler = default!,
        CancellationToken cancellationToken = default)
    {
        var mode = ModeMapper.ResolveMode(sport, league);
        var command = new RefreshContestMediaCommand { ContestId = id, Sport = mode };
        var result = await handler.ExecuteAsync(command, cancellationToken);

        return result.ToActionResult();
    }
}
