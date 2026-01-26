using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Extensions;
using SportsData.Producer.Application.Competitions.Commands.EnqueueCompetitionMediaRefresh;
using SportsData.Producer.Application.Competitions.Commands.EnqueueCompetitionMetricsCalculation;
using SportsData.Producer.Application.Competitions.Commands.RefreshCompetitionDrives;
using SportsData.Producer.Application.Competitions.Commands.RefreshCompetitionMedia;
using SportsData.Producer.Application.Competitions.Commands.RefreshCompetitionMetrics;

namespace SportsData.Producer.Application.Competitions;

[Route("api/competitions")]
[ApiController]
public class CompetitionController : ControllerBase
{
    [HttpPost("{competitionId}/metrics/generate")]
    public async Task<ActionResult<Guid>> GenerateMetrics(
        [FromRoute] Guid competitionId,
        [FromServices] IEnqueueCompetitionMetricsCalculationCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new EnqueueCompetitionMetricsCalculationCommand(competitionId);
        var result = await handler.ExecuteAsync(command, cancellationToken);

        return result.ToActionResult();
    }

    [HttpPost("metrics/generate/{seasonYear}")]
    public async Task<ActionResult<RefreshCompetitionMetricsResult>> RefreshCompetitionMetrics(
        [FromRoute] int seasonYear,
        [FromServices] IRefreshCompetitionMetricsCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new RefreshCompetitionMetricsCommand(seasonYear);
        var result = await handler.ExecuteAsync(command, cancellationToken);

        return result.ToActionResult();
    }

    [HttpPost("{competitionId}/drives/refresh")]
    public async Task<ActionResult<Guid>> RefreshDrives(
        [FromRoute] Guid competitionId,
        [FromServices] IRefreshCompetitionDrivesCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new RefreshCompetitionDrivesCommand(competitionId);
        var result = await handler.ExecuteAsync(command, cancellationToken);

        return result.ToActionResult();
    }

    [HttpPost("{competitionId}/media/refresh")]
    public async Task<ActionResult<Guid>> RefreshMedia(
        [FromRoute] Guid competitionId,
        [FromServices] IEnqueueCompetitionMediaRefreshCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new EnqueueCompetitionMediaRefreshCommand(competitionId, RemoveExisting: true);
        var result = await handler.ExecuteAsync(command, cancellationToken);

        return result.ToActionResult();
    }

    [HttpPost("media/refresh")]
    public async Task<ActionResult<RefreshAllCompetitionMediaResult>> RefreshAllMedia(
        [FromBody] RefreshAllCompetitionMediaCommand command,
        [FromServices] IRefreshAllCompetitionMediaCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(command, cancellationToken);

        return result.ToActionResult();
    }
}
