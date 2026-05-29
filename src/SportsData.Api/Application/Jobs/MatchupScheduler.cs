using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.PickemGroups;
using SportsData.Api.Application.Processors;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Common.Jobs;
using SportsData.Core.Infrastructure.Clients.Season;
using SportsData.Core.Processing;

using System.Linq.Expressions;

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
        private readonly IDateTimeProvider _dateTimeProvider;

        public MatchupScheduler(
            ILogger<MatchupScheduler> logger,
            AppDataContext dataContext,
            ISeasonClientFactory seasonClientFactory,
            IProvideBackgroundJobs backgroundJobProvider,
            IDateTimeProvider dateTimeProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _seasonClientFactory = seasonClientFactory;
            _backgroundJobProvider = backgroundJobProvider;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task ExecuteAsync()
        {
            _logger.LogInformation("{JobName} Began", nameof(MatchupScheduler));

            var now = _dateTimeProvider.UtcNow();
            var inWindow = InWindowPredicate(now);

            // Discover sports with at least one in-window, active league.
            // Pre-PR-D this picked up every sport that had *any* league,
            // including future-start ones — which then produced orphan
            // PickemGroupWeek rows for the sport's current week despite
            // the league not having started yet (the daily-orphan half
            // of the motivating bug — see docs/league-creation-hardening.md).
            var activeSports = await _dataContext.PickemGroups
                .Where(inWindow)
                .Select(g => g.Sport)
                .Distinct()
                .ToListAsync();

            _logger.LogInformation(
                "Found {Count} active sport(s) with in-window leagues: {Sports}",
                activeSports.Count, string.Join(", ", activeSports));

            foreach (var sport in activeSports)
            {
                await ScheduleForSportAsync(sport, inWindow);
            }

            _logger.LogInformation("{JobName} Ended", nameof(MatchupScheduler));
        }

        private async Task ScheduleForSportAsync(Sport sport, Expression<Func<PickemGroup, bool>> inWindow)
        {
            var weekResult = await _seasonClientFactory.Resolve(sport).GetCurrentSeasonWeek();

            if (!weekResult.IsSuccess)
            {
                _logger.LogWarning(
                    "Current week could not be resolved for {Sport}; skipping matchup scheduling for this sport.",
                    sport);
                return;
            }

            var currentWeek = weekResult.Value;

            // In-window + active leagues for this sport. Loading Weeks for
            // the postseason ordinal lookup in the factory + the existence
            // check below. Window filter mirrors the active-sport
            // discovery above so a sport with leagues that all fell out of
            // window between calls is a safe no-op (rather than a NPE on
            // currentWeek and an unnecessary HTTP call to Producer).
            var groups = await _dataContext.PickemGroups
                .Include(x => x.Weeks)
                .Where(x => x.Sport == sport)
                .Where(inWindow)
                .OrderBy(x => x.CreatedUtc)
                .ToListAsync();

            foreach (var group in groups)
            {
                var groupWeek = group.Weeks.FirstOrDefault(x => x.SeasonWeekId == currentWeek.Id);

                if (groupWeek is null)
                {
                    groupWeek = PickemGroupWeekFactory.Create(group, currentWeek);
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

        /// <summary>
        /// A league is "in window" — eligible for matchup-scheduling — when:
        /// <list type="bullet">
        ///   <item><c>DeactivatedUtc</c> is null (not closed by a commissioner
        ///         or auto-deactivated after all contests finalized)</item>
        ///   <item><c>StartsOn</c> is null or already past (future-start
        ///         leagues defer to this gate; eager-bootstrap is PR-B's job)</item>
        ///   <item><c>EndsOn</c> is null or still in the future (closed-window
        ///         leagues stop accepting new <c>PickemGroupWeek</c> rows)</item>
        /// </list>
        /// Built as an <see cref="Expression{TDelegate}"/> so EF Core
        /// translates it into SQL — embedding the same shape inline in both
        /// the sport-discovery query and the per-sport groups query keeps
        /// the gate single-sourced without a static helper that EF can't
        /// parse.
        /// </summary>
        private static Expression<Func<PickemGroup, bool>> InWindowPredicate(DateTime now) =>
            g => g.DeactivatedUtc == null
                && (g.StartsOn == null || g.StartsOn <= now)
                && (g.EndsOn == null || g.EndsOn >= now);
    }
}

public record ScheduleGroupWeekMatchupsCommand(
    Guid GroupId,
    Guid SeasonWeekId,
    int SeasonYear,
    int SeasonWeek,
    bool IsNonStandardWeek,
    Guid CorrelationId);
