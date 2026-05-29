using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Season;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.Processors
{
    public interface IBootstrapLeagueMatchups
    {
        Task Process(BootstrapLeagueMatchupsCommand command);
    }

    /// <summary>
    /// Creation-time orchestrator for a newly-created league. Resolves which
    /// <see cref="SeasonWeek"/>(s) the league overlaps and fans out one
    /// <see cref="ScheduleGroupWeekMatchupsCommand"/> per resolved week to the
    /// existing per-week worker (<see cref="MatchupScheduleProcessor"/>).
    ///
    /// <para>
    /// Replaces the prior model where the
    /// <c>PickemGroupCreatedHandler</c> looked up "current week" directly and
    /// created a single <c>PickemGroupWeek</c> for it — which produced orphan
    /// empty rows for any windowed league whose <c>[StartsOn, EndsOn]</c>
    /// didn't overlap the current SeasonWeek. See
    /// <c>docs/league-creation-matrix.md</c> for the design space this
    /// dispatch covers.
    /// </para>
    ///
    /// <para>
    /// Owns the dispatch only; per-week work (find-or-create shell, fetch
    /// matchups, filter, write) stays in <c>MatchupScheduleProcessor</c>,
    /// which the daily <c>MatchupScheduler</c> also drives. One per-week
    /// worker, two upstream producers (this and the scheduler).
    /// </para>
    /// </summary>
    public class BootstrapLeagueMatchupsProcessor : IBootstrapLeagueMatchups
    {
        // Defensive upper bound on the date-range query for partial windows
        // with `EndsOn = null`. Realistic leagues don't span more than a year;
        // capping at 365 days bounds the result size and the
        // SeasonWeek-overlap query's worst case.
        private static readonly TimeSpan PartialWindowMaxSpan = TimeSpan.FromDays(365);

        private readonly ILogger<BootstrapLeagueMatchupsProcessor> _logger;
        private readonly AppDataContext _dataContext;
        private readonly ISeasonClientFactory _seasonClientFactory;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;
        private readonly IDateTimeProvider _dateTimeProvider;

        public BootstrapLeagueMatchupsProcessor(
            ILogger<BootstrapLeagueMatchupsProcessor> logger,
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

        public async Task Process(BootstrapLeagueMatchupsCommand command)
        {
            var group = await _dataContext.PickemGroups
                .Where(x => x.Id == command.GroupId)
                .FirstOrDefaultAsync();

            if (group is null)
            {
                _logger.LogError(
                    "Bootstrap requested for unknown group {GroupId}; nothing to do.",
                    command.GroupId);
                return;
            }

            var weeks = await ResolveTargetWeeksAsync(group);

            if (weeks.Count == 0)
            {
                _logger.LogInformation(
                    "Bootstrap for group {GroupId} resolved to zero SeasonWeeks ({StartsOn} → {EndsOn}); deferring to MatchupScheduler.",
                    group.Id, group.StartsOn, group.EndsOn);
                return;
            }

            _logger.LogInformation(
                "Bootstrap for group {GroupId} resolved {Count} SeasonWeek(s); enqueuing per-week matchup scheduling.",
                group.Id, weeks.Count);

            foreach (var week in weeks)
            {
                var perWeekCommand = new ScheduleGroupWeekMatchupsCommand(
                    group.Id,
                    week.Id,
                    week.SeasonYear,
                    week.WeekNumber,
                    week.IsNonStandardWeek,
                    command.CorrelationId);

                _backgroundJobProvider.Enqueue<IScheduleGroupWeekMatchups>(
                    p => p.Process(perWeekCommand));
            }
        }

        /// <summary>
        /// Decide which <see cref="CanonicalSeasonWeekDto"/>(s) the league
        /// needs. Full-season leagues (both window bounds null) bootstrap the
        /// current week only and let the daily scheduler advance weekly.
        /// Windowed leagues hit the date-range endpoint with effective
        /// from/to derived from <c>StartsOn</c> / <c>EndsOn</c> and the
        /// 365-day partial-window cap.
        /// </summary>
        private async Task<List<CanonicalSeasonWeekDto>> ResolveTargetWeeksAsync(PickemGroup group)
        {
            var seasonClient = _seasonClientFactory.Resolve(group.Sport);

            if (!group.StartsOn.HasValue && !group.EndsOn.HasValue)
            {
                var currentResult = await seasonClient.GetCurrentSeasonWeek();
                if (!currentResult.IsSuccess)
                {
                    // Transient (between weeks) or permanent (off-season) — let
                    // Hangfire's retry policy handle. If the sport is permanently
                    // off-season the retry budget will eventually exhaust and the
                    // job lands in failed state; a human re-enqueues once the
                    // new season starts.
                    _logger.LogError(
                        "Current week unavailable for {Sport} (group {GroupId}); throwing to retry.",
                        group.Sport, group.Id);
                    throw new InvalidOperationException(
                        $"Current week unavailable for sport {group.Sport}.");
                }

                return [currentResult.Value];
            }

            var (from, to) = ResolveDateRange(group);

            if (from > to)
            {
                // Permanent input error: the league's window resolved to an
                // inverted range. The PR-B validator blocks this at creation
                // (EffectiveEndsOn > now AND StartsOn < EffectiveEndsOn) but
                // it can still surface here if EndsOn was set, StartsOn was
                // null, and the clock rolled past EndsOn before the Hangfire
                // job ran — or if an admin edited the entity into a bad
                // state post-creation. Retrying won't help; log and return
                // an empty list so the caller treats it like row 11
                // ("nothing to bootstrap") rather than DLQ'ing the job.
                _logger.LogError(
                    "Inverted date range for {Sport} (group {GroupId}, [{From}, {To}]); skipping bootstrap.",
                    group.Sport, group.Id, from, to);
                return [];
            }

            var rangeResult = await seasonClient.GetSeasonWeeksOverlapping(from, to);

            if (!rangeResult.IsSuccess)
            {
                _logger.LogError(
                    "Date-range lookup failed for {Sport} (group {GroupId}, [{From}, {To}]); throwing to retry.",
                    group.Sport, group.Id, from, to);
                throw new InvalidOperationException(
                    $"Season-week range unavailable for sport {group.Sport}.");
            }

            return rangeResult.Value;
        }

        /// <summary>
        /// Convert the league's nullable <c>(StartsOn, EndsOn)</c> into a
        /// concrete <c>[from, to]</c> range usable against the date-range
        /// endpoint. Open bounds resolve to "now" (lower) or "from + 365d"
        /// (upper) per DP-3.
        /// </summary>
        private (DateTime From, DateTime To) ResolveDateRange(PickemGroup group)
        {
            var now = _dateTimeProvider.UtcNow();
            var from = group.StartsOn ?? now;
            var to = group.EndsOn ?? from + PartialWindowMaxSpan;
            return (from, to);
        }
    }
}

public record BootstrapLeagueMatchupsCommand(
    Guid GroupId,
    Guid CorrelationId);
