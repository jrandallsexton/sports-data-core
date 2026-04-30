using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
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
        [FromServices] IAppMode appMode,
        CancellationToken cancellationToken)
    {
        // Football-only handler. Resolved manually after the mode guard so
        // non-football pods return a typed BadRequest instead of bubbling a
        // DI resolution InvalidOperationException out of model binding.
        if (appMode.CurrentSport is not (Sport.FootballNcaa or Sport.FootballNfl))
            return BadRequest($"RefreshCompetitionMetrics is football-only. CurrentSport={appMode.CurrentSport}");

        var handler = HttpContext.RequestServices.GetRequiredService<IRefreshCompetitionMetricsCommandHandler>();
        var command = new RefreshCompetitionMetricsCommand(seasonYear);
        var result = await handler.ExecuteAsync(command, cancellationToken);

        return result.ToActionResult();
    }

    [HttpPost("{competitionId}/drives/refresh")]
    public async Task<ActionResult<Guid>> RefreshDrives(
        [FromRoute] Guid competitionId,
        [FromServices] IAppMode appMode,
        CancellationToken cancellationToken)
    {
        if (appMode.CurrentSport is not (Sport.FootballNcaa or Sport.FootballNfl))
            return BadRequest($"RefreshDrives is football-only. CurrentSport={appMode.CurrentSport}");

        var handler = HttpContext.RequestServices.GetRequiredService<IRefreshCompetitionDrivesCommandHandler>();
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
