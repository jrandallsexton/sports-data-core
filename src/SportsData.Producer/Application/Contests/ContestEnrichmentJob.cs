using Microsoft.EntityFrameworkCore;

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

    public class ContestEnrichmentJob<TDataContext> : IContestEnrichmentJob, IAmARecurringJob
        where TDataContext : TeamSportDataContext
    {
        private readonly ILogger<ContestEnrichmentJob<TDataContext>> _logger;
        private readonly TDataContext _dataContext;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public ContestEnrichmentJob(
            ILogger<ContestEnrichmentJob<TDataContext>> logger,
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
                ["JobName"] = "ContestEnrichmentJob",
                ["JobRunId"] = jobRunId
            });

            _logger.LogInformation(
                "ContestEnrichmentJob starting. JobRunId={JobRunId}, NowUtc={NowUtc}",
                jobRunId, DateTime.UtcNow);

            // get the current and previous season week
            var seasonWeeks = await _dataContext.SeasonWeeks
                .AsNoTracking()
                .Where(sw => sw.StartDate < DateTime.UtcNow &&
                             sw.EndDate > DateTime.UtcNow)
                .OrderByDescending(sw => sw.StartDate)
                .Take(2)
                .ToListAsync();

            if (!seasonWeeks.Any())
            {
                _logger.LogError(
                    "Could not determine current season week. NowUtc={NowUtc}",
                    DateTime.UtcNow);
                return;
            }

            _logger.LogInformation(
                "Found {SeasonWeekCount} season week(s) to process. SeasonWeeks={@SeasonWeeks}",
                seasonWeeks.Count,
                seasonWeeks.Select(sw => new
                {
                    sw.Id,
                    sw.Number,
                    sw.StartDate,
                    sw.EndDate
                }));

            var totalEnqueued = 0;
            var totalSkipped = 0;

            foreach (var seasonWeek in seasonWeeks)
            {
                // get contests that have not been finalized
                var contests = await _dataContext.Contests
                    .AsNoTracking()
                    .Where(c => c.SeasonWeekId == seasonWeek.Id &&
                                c.StartDateUtc < DateTime.UtcNow.AddHours(3) &&
                                c.FinalizedUtc == null)
                    .OrderBy(c => c.StartDateUtc)
                    .ToListAsync();

                _logger.LogInformation(
                    "Season week {SeasonWeekId} (week {WeekNumber}): {ContestCount} non-finalized contest(s) to enrich.",
                    seasonWeek.Id, seasonWeek.Number, contests.Count);

                if (contests.Count == 0)
                {
                    continue;
                }

                // spawn a job to finalize each
                foreach (var contest in contests)
                {
                    try
                    {
                        var cmd = new EnrichContestCommand(contest.Id, Guid.NewGuid());
                        var hangfireJobId = _backgroundJobProvider.Enqueue<IEnrichContests>(p => p.Process(cmd));
                        totalEnqueued++;

                        _logger.LogInformation(
                            "Enqueued enrichment for ContestId={ContestId}, ContestName={ContestName}, " +
                            "StartDateUtc={StartDateUtc}, SeasonWeekId={SeasonWeekId}, HangfireJobId={HangfireJobId}, " +
                            "EnrichCorrelationId={EnrichCorrelationId}",
                            contest.Id, contest.Name, contest.StartDateUtc,
                            seasonWeek.Id, hangfireJobId, cmd.CorrelationId);
                    }
                    catch (Exception ex)
                    {
                        totalSkipped++;
                        _logger.LogError(
                            ex,
                            "Failed to enqueue enrichment. ContestId={ContestId}, ContestName={ContestName}, " +
                            "SeasonWeekId={SeasonWeekId}",
                            contest.Id, contest.Name, seasonWeek.Id);
                    }
                }
            }

            _logger.LogInformation(
                "ContestEnrichmentJob completed. JobRunId={JobRunId}, SeasonWeeksProcessed={SeasonWeekCount}, " +
                "TotalEnqueued={TotalEnqueued}, TotalSkipped={TotalSkipped}",
                jobRunId, seasonWeeks.Count, totalEnqueued, totalSkipped);
        }
    }
}
