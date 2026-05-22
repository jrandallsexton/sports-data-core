using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Processors;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Common.Jobs;
using SportsData.Core.Infrastructure.Clients.Season;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.Jobs
{
    /// <summary>
    /// Generates next-week PickemGroupWeek + matchup-schedule jobs for every
    /// active league.
    ///
    /// Sport-agnostic by construction: discovers the active sport list at
    /// runtime from <c>PickemGroups.Select(g => g.Sport).Distinct()</c>, then
    /// resolves each sport's current week through its own
    /// <see cref="ISeasonClientFactory"/>-routed client. Sports with no active
    /// leagues are skipped; sports whose Producer can't report a current week
    /// (offseason, transient failure) are skipped with a warning.
    ///
    /// This is the only scoring/scheduling job NOT in the event-driven primary
    /// path — matchups must be generated BEFORE games happen, so this stays a
    /// cron-driven primary. Daily cadence is sufficient since week boundaries
    /// move at most once per week per sport.
    /// </summary>
    public class MatchupScheduler : IAmARecurringJob
    {
        private readonly ILogger<MatchupScheduler> _logger;
        private readonly AppDataContext _dataContext;
        private readonly ISeasonClientFactory _seasonClientFactory;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public MatchupScheduler(
            ILogger<MatchupScheduler> logger,
            AppDataContext dataContext,
            ISeasonClientFactory seasonClientFactory,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _seasonClientFactory = seasonClientFactory;
            _backgroundJobProvider = backgroundJobProvider;
        }

        public async Task ExecuteAsync()
        {
            _logger.LogInformation("{JobName} Began", nameof(MatchupScheduler));

            // Discover sports with at least one league. Adding a new sport
            // becomes "create a league" — no code change to this job.
            var activeSports = await _dataContext.PickemGroups
                .Select(g => g.Sport)
                .Distinct()
                .ToListAsync();

            _logger.LogInformation(
                "Found {Count} active sport(s) with leagues: {Sports}",
                activeSports.Count, string.Join(", ", activeSports));

            foreach (var sport in activeSports)
            {
                await ScheduleForSportAsync(sport);
            }

            _logger.LogInformation("{JobName} Ended", nameof(MatchupScheduler));
        }

        private async Task ScheduleForSportAsync(Sport sport)
        {
            var weekResult = await _seasonClientFactory.Resolve(sport).GetCurrentSeasonWeek();
            var currentWeek = weekResult.IsSuccess ? weekResult.Value : null;

            if (currentWeek is null)
            {
                _logger.LogWarning(
                    "Current week could not be resolved for {Sport}; skipping matchup scheduling for this sport.",
                    sport);
                return;
            }

            // Only this sport's leagues. Loading Weeks for the ordinal lookup
            // below + the existence check.
            var groups = await _dataContext.PickemGroups
                .Include(x => x.Weeks)
                .Where(x => x.Sport == sport)
                .OrderBy(x => x.CreatedUtc)
                .ToListAsync();

            foreach (var group in groups)
            {
                var groupWeek = group.Weeks.FirstOrDefault(x => x.SeasonWeekId == currentWeek.Id);

                if (groupWeek is null)
                {
                    var currentWeekOrdinal = currentWeek.WeekNumber;

                    // if current week is postseason, we need to change the ordinal week number to reflect that
                    if (currentWeek.IsPostSeason)
                    {
                        // we need to get the number of regular season weeks for this season year
                        currentWeekOrdinal = group.Weeks.OrderByDescending(x => x.SeasonWeek)
                            .Take(1).FirstOrDefault()?.SeasonWeek + 1 ?? currentWeek.WeekNumber;
                    }

                    groupWeek = new PickemGroupWeek()
                    {
                        Id = Guid.NewGuid(),
                        AreMatchupsGenerated = false,
                        GroupId = group.Id,
                        SeasonWeek = currentWeekOrdinal,
                        SeasonYear = currentWeek.SeasonYear,
                        SeasonWeekId = currentWeek.Id,
                        IsNonStandardWeek = currentWeek.IsNonStandardWeek
                    };
                    group.Weeks.Add(groupWeek);
                    await _dataContext.SaveChangesAsync();
                }

                if (!groupWeek.AreMatchupsGenerated)
                {
                    var cmd = new ScheduleGroupWeekMatchupsCommand(
                        group.Id,
                        currentWeek.Id,
                        currentWeek.SeasonYear,
                        currentWeek.WeekNumber,
                        currentWeek.IsNonStandardWeek,
                        Guid.NewGuid());
                    _backgroundJobProvider.Enqueue<IScheduleGroupWeekMatchups>(p => p.Process(cmd));
                }
            }
        }
    }
}

public record ScheduleGroupWeekMatchupsCommand(
    Guid GroupId,
    Guid SeasonWeekId,
    int SeasonYear,
    int SeasonWeek,
    bool IsNonStandardWeek,
    Guid CorrelationId);
