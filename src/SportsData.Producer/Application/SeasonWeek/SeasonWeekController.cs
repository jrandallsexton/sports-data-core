using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Extensions;
using SportsData.Producer.Application.SeasonWeek.Commands.EnqueueSeasonWeekContestsUpdate;

namespace SportsData.Producer.Application.SeasonWeek;

[Route("api/season-weeks")]
[ApiController]
public class SeasonWeekController : ControllerBase
{
    [HttpPost("{seasonWeekId}/update")]
    public async Task<ActionResult<Guid>> UpdateSeasonWeekContests(
        [FromRoute] Guid seasonWeekId,
        [FromServices] IEnqueueSeasonWeekContestsUpdateCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new EnqueueSeasonWeekContestsUpdateCommand(seasonWeekId);
        var result = await handler.ExecuteAsync(command, cancellationToken);

        return result.ToActionResult();
    }
}
