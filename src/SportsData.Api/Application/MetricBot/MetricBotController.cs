using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.UI.Contest.Commands.SubmitContestPredictions;
using SportsData.Api.Application.UI.Contest.Dtos;
using SportsData.Api.Infrastructure.Auth;
using SportsData.Core.Common;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.MetricBot;

namespace SportsData.Api.Application.MetricBot;

/// <summary>
/// Operator entry points to MetricBot, the internal Python deetsMeter service
/// (internal-only, so these proxies are how it is reached on demand). Every
/// action is admin-only: [AdminApiToken] is on the class.
/// </summary>
[ApiController]
[Route("api/metricbot")]
[AdminApiToken]
public class MetricBotController : ApiControllerBase
{
    /// <summary>
    /// Trigger a MetricBot prediction run (deetsMeter). Omit
    /// seasonYear/week for a live run of the current week; supply both
    /// for an experiment/backtest, which never publishes unless
    /// publish=true. MetricBot is internal-only, so this proxy is the
    /// on-demand entry point — the weekly schedule is a Hangfire job.
    ///
    /// Example: POST /api/metricbot/run-week
    /// Body: { "sport": "ncaaf", "seasonYear": 2025, "week": 6,
    ///         "priorSeasonTail": 5, "includeDtos": true }
    /// </summary>
    [HttpPost("run-week")]
    public async Task<ActionResult<MetricBotRunResponse>> RunMetricBotWeek(
        [FromBody] MetricBotRunRequest request,
        [FromServices] IProvideMetricBot metricBot,
        CancellationToken cancellationToken)
    {
        var result = await metricBot.RunWeekAsync(request, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Backtest a historical week: predict it as-of (only information
    /// available entering the week), then grade against final scores —
    /// SU accuracy vs baselines, ATS with pushes excluded-and-counted,
    /// model-vs-market margin MAE, Brier + calibration deciles. Never
    /// publishes predictions.
    ///
    /// Example: POST /api/metricbot/backtest
    /// Body: { "sport": "FootballNcaa", "seasonYear": 2025, "week": 6,
    ///         "priorSeasonTail": 5 }
    /// </summary>
    [HttpPost("backtest")]
    public async Task<ActionResult<MetricBotBacktestResponse>> BacktestMetricBotWeek(
        [FromBody] MetricBotBacktestRequest request,
        [FromServices] IProvideMetricBot metricBot,
        CancellationToken cancellationToken)
    {
        var result = await metricBot.BacktestAsync(request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("health")]
    public async Task<IActionResult> GetMetricBotHealth(
        [FromServices] IProvideMetricBot metricBot,
        CancellationToken cancellationToken)
    {
        var healthy = await metricBot.IsHealthyAsync(cancellationToken);
        return healthy ? Ok(new { status = "healthy" }) : StatusCode(503, new { status = "unreachable" });
    }

    /// <summary>
    /// MetricBot's ingestion endpoint: the Python service POSTs a run's
    /// predictions here as picks for its synthetic user (metricbot/api.py).
    /// </summary>
    [HttpPost("predictions/{syntheticId}")]
    public async Task<IActionResult> PostBulkPicks(
        [FromRoute] string syntheticId,
        [FromBody] List<ContestPredictionDto> predictions,
        [FromServices] ISubmitContestPredictionsCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(syntheticId);

        var command = new SubmitContestPredictionsCommand
        {
            UserId = userId,
            Predictions = predictions
        };

        var result = await handler.ExecuteAsync(command, cancellationToken);

        if (result.IsSuccess)
            return Created();

        return BadRequest();
    }
}
