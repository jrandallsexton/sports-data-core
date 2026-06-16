using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Jobs;
using SportsData.Core.Processing;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Contests
{
    // Same Hangfire reflection-resolve issue that bit ContestUpdateJob — register against
    // this non-generic interface so the recurring-job entry stores a stable type name.
    public interface IContestEnrichmentJob
    {
        Task ExecuteAsync();
    }

    /// <summary>
    /// Sweep-style backstop for the live <see cref="EnrichContestCommand"/> path.
    ///
    /// Primary enrichment trigger is event-driven:
    /// <c>ContestCompletedHandler</c> consumes <c>ContestCompleted</c> from the
    /// streamer and schedules <see cref="IEnrichContests"/> after a 30s delay
    /// (see docs/contest-finalization-event-restructure.md).
    ///
    /// This job is the sweep that picks up everything that didn't go through
    /// the event-driven path: historical contests sourced from a backfill, games
    /// where the event was lost in transit, pod restarts during the publish
    /// window, etc. The filter is intentionally minimal — every contest that
    /// has started, is not yet finalized, and is not terminally cancelled is
    /// a candidate. The sport-specific enrichment processor short-circuits
    /// cleanly on non-STATUS_FINAL, so non-ready candidates are cheap no-ops.
    ///
    /// See docs/contest-enrichment-historical-sweep.md.
    /// </summary>
    public class ContestEnrichmentJob<TDataContext> : IContestEnrichmentJob, IAmARecurringJob
        where TDataContext : TeamSportDataContext
    {
        private readonly ILogger<ContestEnrichmentJob<TDataContext>> _logger;
        private readonly TDataContext _dataContext;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;
        private readonly IDateTimeProvider _dateTimeProvider;

        public ContestEnrichmentJob(
            ILogger<ContestEnrichmentJob<TDataContext>> logger,
            TDataContext dataContext,
            IProvideBackgroundJobs backgroundJobProvider,
            IDateTimeProvider dateTimeProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _backgroundJobProvider = backgroundJobProvider;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task ExecuteAsync()
        {
            var jobRunId = Guid.NewGuid();
            var nowUtc = _dateTimeProvider.UtcNow();

            using var _ = _logger.BeginScope(new Dictionary<string, object>
            {
                ["JobName"] = "ContestEnrichmentJob",
                ["JobRunId"] = jobRunId
            });

            _logger.LogInformation(
                "ContestEnrichmentJob starting. JobRunId={JobRunId}, NowUtc={NowUtc}",
                jobRunId, nowUtc);

            // Unbounded sweep — every contest past its start time that is
            // neither finalized nor terminally cancelled. After the initial
            // historical sweep (manually triggered post-deploy), the daily
            // steady-state volume is small: only contests whose StartDateUtc
            // crossed `now` since yesterday's run AND that the event-driven
            // ContestCompletedHandler didn't already catch.
            var contests = await _dataContext.Contests
                .AsNoTracking()
                .Where(c => c.FinalizedUtc == null
                         && c.CancelledUtc == null
                         && c.StartDateUtc < nowUtc)
                .OrderBy(c => c.StartDateUtc)
                .ToListAsync();

            _logger.LogInformation(
                "ContestEnrichmentJob: {ContestCount} non-finalized, non-cancelled contest(s) with past start time. JobRunId={JobRunId}",
                contests.Count, jobRunId);

            if (contests.Count == 0)
            {
                return;
            }

            var totalEnqueued = 0;
            var totalSkipped = 0;

            foreach (var contest in contests)
            {
                try
                {
                    var cmd = new EnrichContestCommand(contest.Id, Guid.NewGuid());
                    var hangfireJobId = _backgroundJobProvider.Enqueue<IEnrichContests>(p => p.Process(cmd));
                    totalEnqueued++;

                    _logger.LogInformation(
                        "Enqueued enrichment for ContestId={ContestId}, ContestName={ContestName}, " +
                        "StartDateUtc={StartDateUtc}, HangfireJobId={HangfireJobId}, " +
                        "EnrichCorrelationId={EnrichCorrelationId}",
                        contest.Id, contest.Name, contest.StartDateUtc,
                        hangfireJobId, cmd.CorrelationId);
                }
                catch (Exception ex)
                {
                    totalSkipped++;
                    _logger.LogError(
                        ex,
                        "Failed to enqueue enrichment. ContestId={ContestId}, ContestName={ContestName}",
                        contest.Id, contest.Name);
                }
            }

            _logger.LogInformation(
                "ContestEnrichmentJob completed. JobRunId={JobRunId}, " +
                "TotalEnqueued={TotalEnqueued}, TotalSkipped={TotalSkipped}",
                jobRunId, totalEnqueued, totalSkipped);
        }
    }
}
