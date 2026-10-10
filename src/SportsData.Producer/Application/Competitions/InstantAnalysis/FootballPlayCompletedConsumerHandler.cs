using Hangfire;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Contests.Football;
using SportsData.Core.Processing;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Competitions.InstantAnalysis;

public interface IFootballPlayCompletedConsumerHandler
{
    /// <summary>
    /// Routed to the live queue by attribute. The queue-targeted
    /// Enqueue(queue, ...) overload falls back to "default" on our Hangfire
    /// PostgreSQL storage (no per-job queue support; verified 2026-10-10), while
    /// [Queue] is honoured, as with the daemon jobs. Hangfire reads it from the
    /// method on the interface type used at the enqueue site.
    /// </summary>
    [Queue(HangfireQueues.Live)]
    Task Process(FootballPlayCompleted evt);
}

/// <summary>
/// Hangfire job (live queue) that analyses one live play. Spawned by
/// <see cref="FootballPlayCompletedConsumer"/>. Gates on the per-game opt-in,
/// then runs <see cref="IPlayAnalyser"/> with no run id and the newest season
/// documents.
/// </summary>
public class FootballPlayCompletedConsumerHandler : IFootballPlayCompletedConsumerHandler
{
    private readonly ILogger<FootballPlayCompletedConsumerHandler> _logger;
    private readonly FootballDataContext _dataContext;
    private readonly IPlayAnalyser _analyser;

    public FootballPlayCompletedConsumerHandler(
        ILogger<FootballPlayCompletedConsumerHandler> logger,
        FootballDataContext dataContext,
        IPlayAnalyser analyser)
    {
        _logger = logger;
        _dataContext = dataContext;
        _analyser = analyser;
    }

    public async Task Process(FootballPlayCompleted evt)
    {
        using var _ = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = evt.CorrelationId,
            ["CausationId"] = evt.CausationId,
            ["ContestId"] = evt.ContestId,
            ["CompetitionId"] = evt.CompetitionId,
            ["PlayId"] = evt.PlayId
        });

        var enabled = await _dataContext.CompetitionStreams
            .AsNoTracking()
            .AnyAsync(x => x.CompetitionId == evt.CompetitionId && x.LiveAnalysisEnabled);

        if (!enabled)
        {
            _logger.LogDebug("Instant analysis not enabled for this competition; skipping play.");
            return;
        }

        // Live analysis: no run id, and each athlete's newest season document.
        await _analyser.Analyse(evt.PlayId, evt.ContestId, evt.CorrelationId, analysisRunId: null, SeasonDocumentRule.Newest);
    }
}
