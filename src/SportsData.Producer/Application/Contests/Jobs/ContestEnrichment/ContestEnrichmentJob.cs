using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Jobs;
using SportsData.Core.Processing;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Contests.Jobs.ContestEnrichment
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
    /// This job is the sweep that picks up recent games that didn't go through
    /// the event-driven path: the event was lost in transit, a pod restarted
    /// during the publish window, etc. Candidates: started within the last
    /// <see cref="LookbackDays"/> days, not yet finalized, not terminally
    /// cancelled. The sport-specific enrichment processor short-circuits
    /// cleanly on non-STATUS_FINAL, so non-ready candidates are cheap no-ops.
    ///
    /// Historical backfills are NOT this job's responsibility; finalize them
    /// per season via POST api/contests/finalize
    /// (FinalizeContestsBySeasonYearHandler). Unbounded, this sweep would
    /// enqueue every historical contest that can never finalize (postponed,
    /// missing Competition, uncancelled COVID games) on every run: ~25k
    /// permanent no-ops a week across NCAA and MLB in prod on 2026-11-05.
    ///
    /// See docs/processing/historical/contest-enrichment-historical-sweep.md.
    /// </summary>
    public class ContestEnrichmentJob<TDataContext> : IContestEnrichmentJob, IAmARecurringJob
        where TDataContext : TeamSportDataContext
    {
        // Two weekly runs of overlap, so one missed or failed run loses nothing.
        // Same horizon as ContestUpdateJob.StrandedWindowDays.
        internal const int LookbackDays = 14;

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

            var windowStartUtc = nowUtc.AddDays(-LookbackDays);

            _logger.LogInformation(
                "ContestEnrichmentJob starting. NowUtc={NowUtc}, WindowStartUtc={WindowStartUtc}",
                nowUtc, windowStartUtc);

            var contests = await _dataContext.Contests
                .AsNoTracking()
                .Where(c => c.FinalizedUtc == null
                         && c.CancelledUtc == null
                         && c.StartDateUtc >= windowStartUtc
                         && c.StartDateUtc < nowUtc)
                .OrderBy(c => c.StartDateUtc)
                .ToListAsync();

            _logger.LogInformation(
                "ContestEnrichmentJob: {ContestCount} non-finalized, non-cancelled contest(s) started in the last {LookbackDays} days.",
                contests.Count, LookbackDays);

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
                "ContestEnrichmentJob completed. TotalEnqueued={TotalEnqueued}, TotalSkipped={TotalSkipped}",
                totalEnqueued, totalSkipped);
        }
    }
}
