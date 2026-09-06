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
        /// The finalized, non-live row to denormalize from: first match in
        /// preference order; for unknown books, one carrying a spread beats
        /// one without. Null when nothing qualifies — callers then leave the
        /// Contest-level ATS/O-U fields alone rather than denormalizing
        /// garbage (a null here on a finalized contest reads as "no line",
        /// not "push").
        /// </summary>
        public static CompetitionOdds? SelectPrimary(IEnumerable<CompetitionOdds>? allOdds)
        {
            if (allOdds is null) return null;

            var candidates = allOdds
                .Where(o => o.FinalizedUtc.HasValue && !LiveOddsProviderIds.Contains(o.ProviderId))
                .ToList();

            foreach (var id in PreferredProviderIds)
            {
                var match = candidates.FirstOrDefault(o => o.ProviderId == id);
                if (match != null) return match;
            }

            return candidates.FirstOrDefault(o => o.Spread.HasValue)
                   ?? candidates.FirstOrDefault();
        }
    }
}
