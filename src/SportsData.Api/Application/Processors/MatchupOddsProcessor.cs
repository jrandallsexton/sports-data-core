using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Contests;

namespace SportsData.Api.Application.Processors
{
    /// <summary>Apply one contest's displayed odds to every PickemGroupMatchup carrying it.</summary>
    public record ApplyMatchupOddsCommand(
        Guid ContestId,
        Sport Sport,
        DisplayedContestOdds Odds,
        Guid CorrelationId);

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
    /// </summary>
    public class MatchupOddsProcessor : IApplyMatchupOdds
    {
        private readonly AppDataContext _dataContext;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ILogger<MatchupOddsProcessor> _logger;

        public MatchupOddsProcessor(
            AppDataContext dataContext,
            IDateTimeProvider dateTimeProvider,
            ILogger<MatchupOddsProcessor> logger)
        {
            _dataContext = dataContext;
            _dateTimeProvider = dateTimeProvider;
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

            foreach (var m in matchups)
            {
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

                // Stamp only when EF detected a change, as MatchupScheduleProcessor
                // does: a rewrite must leave a trace, a no-op must not.
                if (_dataContext.Entry(m).State == EntityState.Modified)
                {
                    m.ModifiedUtc = _dateTimeProvider.UtcNow();
                    m.ModifiedBy = Guid.Empty;
                }
            }

            await _dataContext.SaveChangesAsync();

            _logger.LogInformation(
                "Displayed odds from provider {ProviderId} applied to {Count} matchups.",
                odds.ProviderId, matchups.Count);
        }
    }
}
