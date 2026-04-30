using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Jobs;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Processing;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Contests
{
    // Hangfire stores the job type by name and resolves it via reflection on each
    // recurring trigger. Closed-generic types like ContestUpdateJob`1[BaseballDataContext]
    // do not always round-trip through that resolver. Register and resolve through this
    // non-generic interface instead — DI binds it per-sport to the correct closed generic.
    public interface IContestUpdateJob
    {
        Task ExecuteAsync();
    }

    /// <summary>
    /// Gets a list of contestIds for the current season week that need to be updated
    /// and enqueues jobs to update them.
    /// </summary>
    public class ContestUpdateJob<TDataContext> : IContestUpdateJob, IAmARecurringJob
        where TDataContext : TeamSportDataContext
    {
        // One-shot backfill: when true, ignore current-season-week scoping and enqueue
        // updates for every non-finalized contest in the most recent season whose start
        // time has passed. Used to catch MLB up after mid-season onboarding. Flip back
        // to false before steady-state deploys — the default per-week path is what runs
        // in production.
        private static readonly bool BackfillCurrentSeason = true;

        private readonly ILogger<ContestUpdateJob<TDataContext>> _logger;
        private readonly TDataContext _dataContext;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;
        private readonly IAppMode _appMode;

        public ContestUpdateJob(
            ILogger<ContestUpdateJob<TDataContext>> logger,
            TDataContext dataContext,
            IProvideBackgroundJobs backgroundJobProvider,
            IAppMode appMode)
        {
            _logger = logger;
            _dataContext = dataContext;
            _backgroundJobProvider = backgroundJobProvider;
            _appMode = appMode;
        }

        public async Task ExecuteAsync()
        {
            var correlationId = Guid.NewGuid();
            var startTime = DateTime.UtcNow;

            using (_logger.BeginScope(new Dictionary<string, object>
                   {
                       ["CorrelationId"] = correlationId,
                       ["JobName"] = "ContestUpdateJob"
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
            if (BackfillCurrentSeason)
            {
                await ExecuteBackfillCurrentSeason(correlationId);
                return;
            }

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
                "📋 CONTEST_DETAILS: ContestBase summary. Contests={@Contests}",
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
                        _appMode.CurrentSport,
                        correlationId); // Use same correlation ID for all updates in this job run
                    
                    var jobId = _backgroundJobProvider.Enqueue<IUpdateContests>(p => p.Process(cmd));
                    
                    enqueuedCount++;
                    
                    _logger.LogDebug(
                        "✅ CONTEST_ENQUEUED: ContestBase update job enqueued. ContestId={ContestId}, " +
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
                "📊 ENQUEUE_SUMMARY: ContestBase update jobs enqueued. Total={Total}, Succeeded={Succeeded}, " +
                "Failed={Failed}, SeasonWeekId={SeasonWeekId}, CorrelationId={CorrelationId}",
                contests.Count,
                enqueuedCount,
                failedCount,
                currentSeasonWeek.Id,
                correlationId);
        }

        private async Task ExecuteBackfillCurrentSeason(Guid correlationId)
        {
            _logger.LogWarning(
                "🛠️ BACKFILL_MODE: BackfillCurrentSeason flag is ON. Bypassing current-season-week scope. " +
                "CurrentUtc={CurrentUtc}, CorrelationId={CorrelationId}",
                DateTime.UtcNow,
                correlationId);

            var currentSeasonYear = await _dataContext.Seasons
                .AsNoTracking()
                .OrderByDescending(s => s.Year)
                .Select(s => (int?)s.Year)
                .FirstOrDefaultAsync();

            if (currentSeasonYear is null)
            {
                _logger.LogError(
                    "❌ SEASON_NOT_FOUND: No seasons found in database. Cannot run backfill. CorrelationId={CorrelationId}",
                    correlationId);
                return;
            }

            _logger.LogInformation(
                "✅ BACKFILL_SEASON: Targeting season for backfill. SeasonYear={SeasonYear}, CorrelationId={CorrelationId}",
                currentSeasonYear,
                correlationId);

            var contests = await _dataContext.Contests
                .AsNoTracking()
                .Where(c => c.SeasonYear == currentSeasonYear &&
                            c.StartDateUtc < DateTime.UtcNow &&
                            c.FinalizedUtc == null)
                .OrderBy(c => c.StartDateUtc)
                .ToListAsync();

            _logger.LogInformation(
                "✅ BACKFILL_CONTESTS_FOUND: Non-finalized started contests for season. Count={Count}, " +
                "SeasonYear={SeasonYear}, CorrelationId={CorrelationId}",
                contests.Count,
                currentSeasonYear,
                correlationId);

            if (contests.Count == 0)
            {
                return;
            }

            var enqueuedCount = 0;
            var failedCount = 0;

            foreach (var contest in contests)
            {
                try
                {
                    var cmd = new UpdateContestCommand(
                        contest.Id,
                        SourceDataProvider.Espn,
                        _appMode.CurrentSport,
                        correlationId);

                    var jobId = _backgroundJobProvider.Enqueue<IUpdateContests>(p => p.Process(cmd));

                    enqueuedCount++;

                    _logger.LogDebug(
                        "✅ BACKFILL_CONTEST_ENQUEUED: ContestId={ContestId}, ShortName={ShortName}, " +
                        "HangfireJobId={JobId}, StartDate={StartDate}",
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
                        "❌ BACKFILL_CONTEST_ENQUEUE_FAILED: ContestId={ContestId}, ShortName={ShortName}, Error={ErrorMessage}",
                        contest.Id,
                        contest.ShortName,
                        ex.Message);
                }
            }

            _logger.LogInformation(
                "📊 BACKFILL_ENQUEUE_SUMMARY: Total={Total}, Succeeded={Succeeded}, Failed={Failed}, " +
                "SeasonYear={SeasonYear}, CorrelationId={CorrelationId}",
                contests.Count,
                enqueuedCount,
                failedCount,
                currentSeasonYear,
                correlationId);
        }
    }
}
