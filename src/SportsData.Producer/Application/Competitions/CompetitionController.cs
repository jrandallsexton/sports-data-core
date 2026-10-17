using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Extensions;
using SportsData.Producer.Application.Competitions.Commands.EnqueueCompetitionMediaRefresh;
using SportsData.Producer.Application.Competitions.Commands.EnqueueCompetitionMetricsCalculation;
using SportsData.Producer.Application.Competitions.Commands.RefreshCompetitionDrives;
using SportsData.Producer.Application.Competitions.Commands.RefreshCompetitionMedia;
using SportsData.Producer.Application.Competitions.Commands.RefreshCompetitionMetrics;
using SportsData.Producer.Application.Competitions.Queries.GetCompetitionsWithoutCompetitors;
using SportsData.Producer.Application.Competitions.Queries.GetCompetitionsWithoutDrives;
using SportsData.Producer.Application.Competitions.Queries.GetCompetitionsWithoutMetrics;
using SportsData.Producer.Application.Competitions.Queries.GetCompetitionsWithoutPlays;
using SportsData.Core.Dtos.Competition;

namespace SportsData.Producer.Application.Competitions;

[Route("api/competitions")]
[ApiController]
public class CompetitionController : ControllerBase
{
    /// <summary>
    /// Diagnostics: this sport's competitions whose competitor count is not 2.
    /// API aggregates every Producer's answer (api/diagnostics/competitions-without-competitors).
    /// </summary>
    [HttpGet("without-competitors")]
    public async Task<ActionResult<List<CompetitionWithoutCompetitorsDto>>> GetCompetitionsWithoutCompetitors(
        [FromServices] IGetCompetitionsWithoutCompetitorsQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(new GetCompetitionsWithoutCompetitorsQuery(), cancellationToken);

        return result.ToActionResult();
    }

    /// <summary>
    /// Diagnostics: started competitions with 10 or fewer plays, for one
    /// season (default: the latest started). FBS-involved only on the NCAA pod.
    /// </summary>
    [HttpGet("without-plays")]
    public async Task<ActionResult<List<CompetitionWithoutPlaysDto>>> GetCompetitionsWithoutPlays(
        [FromQuery] int? seasonYear,
        [FromServices] IGetCompetitionsWithoutPlaysQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(new GetCompetitionsWithoutPlaysQuery(seasonYear), cancellationToken);

        return result.ToActionResult();
    }

    /// <summary>
    /// Diagnostics (football only): started competitions with plays but no
    /// drive links, for one season (default: the latest started).
    /// </summary>
    [HttpGet("without-drives")]
    public async Task<ActionResult<List<CompetitionWithoutDrivesDto>>> GetCompetitionsWithoutDrives(
        [FromQuery] int? seasonYear,
        [FromServices] IAppMode appMode,
        CancellationToken cancellationToken)
    {
        // Football-only handler. Resolved manually after the mode guard so
        // non-football pods return a typed BadRequest instead of a DI
        // resolution InvalidOperationException.
        if (appMode.CurrentSport is not (Sport.FootballNcaa or Sport.FootballNfl))
            return BadRequest($"GetCompetitionsWithoutDrives is football-only. CurrentSport={appMode.CurrentSport}");

        var handler = HttpContext.RequestServices.GetRequiredService<IGetCompetitionsWithoutDrivesQueryHandler>();
        var result = await handler.ExecuteAsync(new GetCompetitionsWithoutDrivesQuery(seasonYear), cancellationToken);

        return result.ToActionResult();
    }

    /// <summary>
    /// Diagnostics: started competitions with no CompetitionMetric rows, for
    /// one season (default: the latest started).
    /// </summary>
    [HttpGet("without-metrics")]
    public async Task<ActionResult<List<CompetitionWithoutMetricsDto>>> GetCompetitionsWithoutMetrics(
        [FromQuery] int? seasonYear,
        [FromServices] IGetCompetitionsWithoutMetricsQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(new GetCompetitionsWithoutMetricsQuery(seasonYear), cancellationToken);

        return result.ToActionResult();
    }

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
