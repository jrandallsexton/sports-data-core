using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Jobs;
using SportsData.Core.Processing;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Contests
{
    /// <summary>
    /// Gets a list of contestIds for the current season week that need to be updated
    /// and enqueues jobs to update them.
    /// </summary>
    public class ContestUpdateJob : IAmARecurringJob
    {
        private readonly ILogger<ContestUpdateJob> _logger;
        private readonly TeamSportDataContext _dataContext;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public ContestUpdateJob(
            ILogger<ContestUpdateJob> logger,
            TeamSportDataContext dataContext,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _backgroundJobProvider = backgroundJobProvider;
        }

        public async Task ExecuteAsync()
        {
            var correlationId = Guid.NewGuid();
            var startTime = DateTime.UtcNow;

            using (_logger.BeginScope(new Dictionary<string, object>
                   {
                       ["CorrelationId"] = correlationId,
                       ["JobName"] = nameof(ContestUpdateJob)
                   }))
            {
                _logger.LogInformation(
                    "🔄 JOB_STARTED: ContestUpdateJob started. CorrelationId={CorrelationId}",
                    correlationId);

                try
                {
                    await ExecuteInternal(correlationId);
                    
                    var duration = DateTime.UtcNow - startTime;
                    _logger.LogInformation(
                        "✅ JOB_COMPLETED: ContestUpdateJob completed successfully. Duration={DurationSeconds}s, CorrelationId={CorrelationId}",
                        duration.TotalSeconds,
                        correlationId);
                }
                catch (Exception ex)
                {
                    var duration = DateTime.UtcNow - startTime;
                    _logger.LogError(
                        ex,
                        "💥 JOB_FAILED: ContestUpdateJob failed. Duration={DurationSeconds}s, CorrelationId={CorrelationId}, Error={ErrorMessage}",
                        duration.TotalSeconds,
                        correlationId,
                        ex.Message);
                    throw;
                }
            }
        }

        private async Task ExecuteInternal(Guid correlationId)
        {
            _logger.LogInformation(
                "📅 QUERY_SEASON_WEEK: Querying for current season week. CurrentUtc={CurrentUtc}",
                DateTime.UtcNow);

            // get the current season week
            var currentSeasonWeek = await _dataContext.SeasonWeeks
                .Include(w => w.Season)
                .AsNoTracking()
                .Where(sw => sw.StartDate < DateTime.UtcNow &&
                             sw.EndDate > DateTime.UtcNow)
                .FirstOrDefaultAsync();

            if (currentSeasonWeek is null)
            {
                _logger.LogError(
                    "❌ SEASON_WEEK_NOT_FOUND: Could not determine current season week. CurrentUtc={CurrentUtc}",
                    DateTime.UtcNow);
                return;
            }

            _logger.LogInformation(
                "✅ SEASON_WEEK_FOUND: Current season week identified. SeasonWeekId={SeasonWeekId}, " +
                "Season={SeasonYear}, Week={WeekNumber}, StartDate={StartDate}, EndDate={EndDate}",
                currentSeasonWeek.Id,
                currentSeasonWeek.Season?.Year ?? 0,
                currentSeasonWeek.Number,
                currentSeasonWeek.StartDate,
                currentSeasonWeek.EndDate);

            _logger.LogInformation(
                "🔍 QUERY_CONTESTS: Querying for non-finalized contests in current week. SeasonWeekId={SeasonWeekId}",
                currentSeasonWeek.Id);

            // get all contests in this season week
            var contests = await _dataContext.Contests
                .AsNoTracking()
                .Where(c => c.SeasonWeekId == currentSeasonWeek.Id && c.FinalizedUtc == null)
                .OrderBy(c => c.StartDateUtc)
                .ToListAsync();

            _logger.LogInformation(
                "✅ CONTESTS_FOUND: Found contests to update. Count={Count}, SeasonWeekId={SeasonWeekId}",
                contests.Count,
                currentSeasonWeek.Id);

            if (contests.Count == 0)
            {
                _logger.LogInformation(
                    "ℹ️ NO_CONTESTS: No non-finalized contests found in current week. SeasonWeekId={SeasonWeekId}",
                    currentSeasonWeek.Id);
                return;
            }

            // Log contest details for visibility
            var contestSummaries = contests.Select(c => new
            {
                c.Id,
                c.ShortName,
                StartDate = c.StartDateUtc,
                IsStarted = c.StartDateUtc < DateTime.UtcNow,
                HoursUntilStart = (c.StartDateUtc - DateTime.UtcNow).TotalHours
            }).ToList();

            _logger.LogInformation(
                "📋 CONTEST_DETAILS: Contest summary. Contests={@Contests}",
                contestSummaries);

            var enqueuedCount = 0;
            var failedCount = 0;

            // spawn a job to update each
            foreach (var contest in contests)
            {
                try
                {
                    var cmd = new UpdateContestCommand(
                        contest.Id,
                        SourceDataProvider.Espn,
                        Sport.FootballNcaa,
                        correlationId); // Use same correlation ID for all updates in this job run
                    
                    var jobId = _backgroundJobProvider.Enqueue<IUpdateContests>(p => p.Process(cmd));
                    
                    enqueuedCount++;
                    
                    _logger.LogDebug(
                        "✅ CONTEST_ENQUEUED: Contest update job enqueued. ContestId={ContestId}, " +
                        "ShortName={ShortName}, HangfireJobId={JobId}, StartDate={StartDate}",
                        contest.Id,
                        contest.ShortName,
                        jobId,
                        contest.StartDateUtc);
                }
                catch (Exception ex)
                {
                    failedCount++;
                    
                    _logger.LogError(
                        ex,
                        "❌ CONTEST_ENQUEUE_FAILED: Failed to enqueue contest update. ContestId={ContestId}, " +
                        "ShortName={ShortName}, Error={ErrorMessage}",
                        contest.Id,
                        contest.ShortName,
                        ex.Message);
                }
            }

            _logger.LogInformation(
                "📊 ENQUEUE_SUMMARY: Contest update jobs enqueued. Total={Total}, Succeeded={Succeeded}, " +
                "Failed={Failed}, SeasonWeekId={SeasonWeekId}, CorrelationId={CorrelationId}",
                contests.Count,
                enqueuedCount,
                failedCount,
                currentSeasonWeek.Id,
                correlationId);
        }
    }
}
