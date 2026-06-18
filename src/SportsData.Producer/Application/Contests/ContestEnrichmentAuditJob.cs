using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Jobs;
using SportsData.Core.Processing;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Contests;

// Same Hangfire reflection-resolve issue that bit ContestUpdateJob and
// ContestEnrichmentJob — register against this non-generic interface so the
// recurring-job entry stores a stable type name across deployments.
public interface IContestEnrichmentAuditJob
{
    Task ExecuteAsync();
}

/// <summary>
/// Nightly sweep that re-verifies finalized contests against the canonical
/// score source. Catches the
/// "FinalizedUtc + SpreadWinner + null Winner" corruption signature
/// (2026-06-18 Rockies @ Cubs) and anything else that drifts after the
/// initial enrichment run.
///
/// Candidate set: <c>FinalizedUtc IS NOT NULL AND AuditedUtc IS NULL</c>.
/// Steady-state, that is yesterday's newly-finalized contests; first-run
/// after deploy is the entire backlog of historical finalized contests,
/// which is why the batch is capped — additional candidates roll over to
/// the next firing.
///
/// Fan-out: enqueues one <see cref="IAuditContestEnrichment"/> per contest
/// so audits parallelize across Hangfire workers. The per-contest
/// processor handles match/mismatch/stale-source logic; this sweep is just
/// the candidate scanner.
///
/// Also exposed via the Hangfire dashboard for manual triggering when
/// chasing a specific corruption.
/// </summary>
public class ContestEnrichmentAuditJob<TDataContext> : IContestEnrichmentAuditJob, IAmARecurringJob
    where TDataContext : TeamSportDataContext
{
    // Cap the per-firing fan-out so the first-run backlog doesn't enqueue
    // tens of thousands of Hangfire jobs at once. Tomorrow's firing picks
    // up where today's left off. Tuned against the ContestEnrichmentJob
    // sweep cadence so the two don't compete for the same worker pool.
    private const int BatchSize = 500;

    private readonly ILogger<ContestEnrichmentAuditJob<TDataContext>> _logger;
    private readonly TDataContext _dataContext;
    private readonly IProvideBackgroundJobs _backgroundJobProvider;

    public ContestEnrichmentAuditJob(
        ILogger<ContestEnrichmentAuditJob<TDataContext>> logger,
        TDataContext dataContext,
        IProvideBackgroundJobs backgroundJobProvider)
    {
        _logger = logger;
        _dataContext = dataContext;
        _backgroundJobProvider = backgroundJobProvider;
    }

    public async Task ExecuteAsync()
    {
        var jobRunId = Guid.NewGuid();

        using var _ = _logger.BeginScope(new Dictionary<string, object>
        {
            ["JobName"] = "ContestEnrichmentAuditJob",
            ["JobRunId"] = jobRunId
        });

        _logger.LogInformation(
            "ContestEnrichmentAuditJob starting. BatchSize={BatchSize}",
            BatchSize);

        // Order by FinalizedUtc so the oldest unverified finalizations are
        // audited first — keeps the backlog draining predictably during
        // the post-deploy ramp.
        var contestIds = await _dataContext.Contests
            .AsNoTracking()
            .Where(c => c.FinalizedUtc != null && c.AuditedUtc == null)
            .OrderBy(c => c.FinalizedUtc)
            .Take(BatchSize)
            .Select(c => c.Id)
            .ToListAsync();

        _logger.LogInformation(
            "ContestEnrichmentAuditJob: {ContestCount} pending audit candidate(s) in this batch.",
            contestIds.Count);

        if (contestIds.Count == 0)
        {
            return;
        }

        var totalEnqueued = 0;
        var totalSkipped = 0;

        foreach (var contestId in contestIds)
        {
            try
            {
                var cmd = new AuditContestEnrichmentCommand(contestId, Guid.NewGuid());
                _backgroundJobProvider.Enqueue<IAuditContestEnrichment>(p => p.Process(cmd));
                totalEnqueued++;
            }
            catch (Exception ex)
            {
                totalSkipped++;
                _logger.LogError(
                    ex,
                    "Failed to enqueue audit. ContestId={ContestId}",
                    contestId);
            }
        }

        _logger.LogInformation(
            "ContestEnrichmentAuditJob completed. TotalEnqueued={TotalEnqueued}, TotalSkipped={TotalSkipped}",
            totalEnqueued, totalSkipped);
    }
}
