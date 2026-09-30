using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekMatchups;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Contests;

namespace SportsData.Api.Application.Processors
{
    /// <summary>Apply one contest's displayed odds to every PickemGroupMatchup carrying it.</summary>
    /// <param name="AsOfUtc">
    /// The odds version: the source event's CreatedUtc, stamped by the Producer
    /// when it built the event. Compared against PickemGroupMatchup.OddsAsOfUtc.
    /// </param>
    public record ApplyMatchupOddsCommand(
        Guid ContestId,
        Sport Sport,
        DisplayedContestOdds Odds,
        Guid CorrelationId,
        DateTime AsOfUtc);

    public interface IApplyMatchupOdds
    {
        Task Process(ApplyMatchupOddsCommand command);
    }

    /// <summary>
    /// Hangfire job enqueued by the ContestOddsCreated / ContestOddsUpdated
    /// consumers when the event carries the DISPLAYED row (the one the matchup
    /// cards read). Writes the whole displayed line onto every matchup for the
    /// contest in a league of that sport: the spread text, home spread (away =
    /// its negation, as in the Producer's matchup SQL), total, over/under
    /// prices, and each team's moneyline and spread price. Writing the line
    /// with its prices keeps a price from ever pairing with a stale line.
    ///
    /// A field is written only when the snapshot carries a value (never
    /// erases), matching the odds-pricing backfill. Pick scoring is unaffected:
    /// it grades against the Producer's final result spread, not these
    /// columns. Idempotent.
    ///
    /// Ordered by version (CodeRabbit, #803): jobs run on many workers and a
    /// failed one is retried with delays reaching hours, so an older snapshot
    /// could otherwise land after a newer one. A matchup is written only when
    /// the command's AsOfUtc is newer than its OddsAsOfUtc, and the version is
    /// recorded even when no value changes, so an older straggler cannot win
    /// later. ModifiedUtc and cache eviction still follow actual value changes.
    /// </summary>
    public class MatchupOddsProcessor : IApplyMatchupOdds
    {
        private readonly AppDataContext _dataContext;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ILeagueWeekMatchupsCache _matchupsCache;
        private readonly ILogger<MatchupOddsProcessor> _logger;

        public MatchupOddsProcessor(
            AppDataContext dataContext,
            IDateTimeProvider dateTimeProvider,
            ILeagueWeekMatchupsCache matchupsCache,
            ILogger<MatchupOddsProcessor> logger)
        {
            _dataContext = dataContext;
            _dateTimeProvider = dateTimeProvider;
            _matchupsCache = matchupsCache;
            _logger = logger;
        }

        public async Task Process(ApplyMatchupOddsCommand command)
        {
            using var _ = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = command.CorrelationId,
                ["ContestId"] = command.ContestId,
                ["Sport"] = command.Sport
            });

            var odds = command.Odds;

            var matchups = await _dataContext.PickemGroupMatchups
                .Where(m => m.ContestId == command.ContestId
                         && _dataContext.PickemGroups.Any(g => g.Id == m.GroupId && g.Sport == command.Sport))
                .ToListAsync();

            if (matchups.Count == 0)
            {
                // Common: most contests with odds are in no league.
                _logger.LogDebug("Displayed odds received for a contest no league carries; nothing to apply.");
                return;
            }

            var homeSpread = (double?)odds.Spread;
            var changedLeagueWeeks = new HashSet<(Guid GroupId, int SeasonWeek)>();

            // Npgsql writes timestamptz only from DateTimeKind.Utc; the value is
            // UTC by construction, but a serializer round trip may drop the kind.
            var asOfUtc = command.AsOfUtc.Kind == DateTimeKind.Utc
                ? command.AsOfUtc
                : DateTime.SpecifyKind(command.AsOfUtc, DateTimeKind.Utc);
            var stale = 0;

            foreach (var m in matchups)
            {
                if (m.OddsAsOfUtc.HasValue && m.OddsAsOfUtc.Value >= asOfUtc)
                {
                    // An equal-or-newer snapshot is already applied.
                    stale++;
                    continue;
                }

                m.Spread = odds.Details ?? m.Spread;
                m.HomeSpread = homeSpread ?? m.HomeSpread;
                m.AwaySpread = homeSpread.HasValue ? -homeSpread.Value : m.AwaySpread;
                m.OverUnder = (double?)odds.OverUnder ?? m.OverUnder;
                m.OverOdds = (double?)odds.OverOdds ?? m.OverOdds;
                m.UnderOdds = (double?)odds.UnderOdds ?? m.UnderOdds;
                m.AwayMoneyLine = odds.AwayMoneyLine ?? m.AwayMoneyLine;
                m.HomeMoneyLine = odds.HomeMoneyLine ?? m.HomeMoneyLine;
                m.AwaySpreadPrice = (double?)odds.AwaySpreadPrice ?? m.AwaySpreadPrice;
                m.HomeSpreadPrice = (double?)odds.HomeSpreadPrice ?? m.HomeSpreadPrice;

                // Asked BEFORE recording the version, so it reflects odds values
                // only. Stamp only when EF detected a value change, as
                // MatchupScheduleProcessor does: a rewrite must leave a trace, a
                // no-op must not.
                if (_dataContext.Entry(m).State == EntityState.Modified)
                {
                    m.ModifiedUtc = _dateTimeProvider.UtcNow();
                    m.ModifiedBy = Guid.Empty;
                    changedLeagueWeeks.Add((m.GroupId, m.SeasonWeek));
                }

                m.OddsAsOfUtc = asOfUtc;
            }

            await _dataContext.SaveChangesAsync();

            // The league-week slate is served from ILeagueWeekMatchupsCache;
            // without eviction members keep reading the pre-change line and
            // prices until the entry expires. Same rule as the schedule and
            // record-audit processors; only weeks that actually changed.
            foreach (var (groupId, seasonWeek) in changedLeagueWeeks)
                await _matchupsCache.RemoveAsync(groupId, seasonWeek);

            _logger.LogInformation(
                "Displayed odds from provider {ProviderId} (as of {AsOfUtc}) applied to {Applied} of {Count} matchups ({Stale} already newer); {Evicted} league-weeks evicted.",
                odds.ProviderId, asOfUtc, matchups.Count - stale, matchups.Count, stale, changedLeagueWeeks.Count);
        }
    }
}
