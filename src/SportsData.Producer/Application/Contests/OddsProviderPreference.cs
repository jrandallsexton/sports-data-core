using SportsData.Producer.Enums;
using SportsData.Producer.Extensions;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Contests
{
    /// <summary>
    /// Which <see cref="CompetitionOdds"/> row is the PRIMARY for
    /// Contest-level denorm (SpreadWinnerFranchiseSeasonId / OverUnder)
    /// and odds reconciliation.
    ///
    /// Two rules, born of Kent State @ South Carolina 2026-09-05 (0-57
    /// against SC -35.5, graded a PUSH because the old
    /// "EspnBet-or-arbitrary-FirstOrDefault" fallback picked the
    /// "DraftKings - Live Odds" row, whose null spread yields a null ATS
    /// winner):
    ///  1. LIVE odds rows are NEVER primary. They are in-game snapshots
    ///     (ESPN deletes them post-game); the pregame closing line is the
    ///     record a pick was made against.
    ///  2. Preference is an ORDERED LIST, not a single id with an
    ///     arbitrary fallback. ESPN's featured book changed from ESPN Bet
    ///     to DraftKings for 2026; either (or both) can appear.
    /// <see cref="Queries.Matchups.MatchupSqlBuilder"/> encodes the same
    /// order for display SQL — keep them aligned.
    /// </summary>
    public static class OddsProviderPreference
    {
        public static readonly string[] PreferredProviderIds =
        {
            SportsBook.EspnBet.ToProviderId(),          // 58
            SportsBook.DraftKings100.ToProviderId(),    // 100
            SportsBook.DraftKings.ToProviderId(),       // 40
        };

        public static readonly string[] LiveOddsProviderIds =
        {
            SportsBook.EspnBetLiveOdds.ToProviderId(),     // 59
            SportsBook.DraftKingsLiveOdds.ToProviderId(),  // 200
        };

        /// <summary>
        /// The finalized, non-live row to denormalize from. Spread presence
        /// outranks book preference: a spread-carrying row from ANY book
        /// beats a spreadless row from a preferred one — EnrichOddsResults
        /// finalizes spreadless (moneyline/O-U-only) rows too, and choosing
        /// one while a real pregame line exists elsewhere recreates the
        /// null-ATS "push" this policy exists to kill (Vortex, PR #732).
        /// Selection: (1) preference order among spread-carrying rows;
        /// (2) any spread-carrying row; (3) no spread anywhere — preference
        /// order among the rest, so O-U still denormalizes from the best
        /// book while ATS stays legitimately null ("no line", not "push");
        /// (4) null when nothing qualifies — callers leave the
        /// Contest-level fields alone.
        /// </summary>
        public static CompetitionOdds? SelectPrimary(IEnumerable<CompetitionOdds>? allOdds)
        {
            if (allOdds is null) return null;

            var candidates = allOdds
                .Where(o => o.FinalizedUtc.HasValue && !LiveOddsProviderIds.Contains(o.ProviderId))
                .ToList();

            foreach (var id in PreferredProviderIds)
            {
                var match = candidates.FirstOrDefault(o => o.ProviderId == id && o.Spread.HasValue);
                if (match != null) return match;
            }

            var anyWithSpread = candidates.FirstOrDefault(o => o.Spread.HasValue);
            if (anyWithSpread != null) return anyWithSpread;

            foreach (var id in PreferredProviderIds)
            {
                var match = candidates.FirstOrDefault(o => o.ProviderId == id);
                if (match != null) return match;
            }

            return candidates.FirstOrDefault();
        }
    }
}
