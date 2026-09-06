using SportsData.Producer.Application.Contests.Queries.Matchups;
using SportsData.Producer.Enums;
using SportsData.Producer.Extensions;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Contests
{
    /// <summary>
    /// Which <see cref="CompetitionOdds"/> row is the PRIMARY for
    /// Contest-level denorm (SpreadWinnerFranchiseSeasonId / OverUnder).
    ///
    /// Contract (Kent State @ South Carolina 2026-09-05 + Vortex rounds on
    /// PR #732):
    ///  1. LIVE odds rows are NEVER primary — in-game snapshots, deleted by
    ///     ESPN post-game; the pregame line is the record.
    ///  2. When ANY row from the DISPLAYED set exists (the providers the
    ///     matchup cards and the API's pick-scoring snapshot read —
    ///     compile-time bound to <see cref="MatchupSqlBuilder"/>), selection
    ///     resolves STRICTLY within that set, spread-carrying first. A
    ///     spreadless displayed row beating a spread-carrying foreign book
    ///     is deliberate: the product showed no line, picks were scored
    ///     straight-up (PickScoringService's null-spread fallback), and the
    ///     contest denorm must agree with what users experienced — an ATS
    ///     winner from a book nobody saw would contradict the pick grades.
    ///  3. Only when NO displayed-set row exists (the historical corpus —
    ///     10,588 contests carry spreads exclusively from era books like
    ///     Westgate/SugarHouse/consensus, measured 2026-09-06) fall back to
    ///     any other non-live book, spread-carrying first, so historical
    ///     re-finalization preserves the corpus ATS record.
    ///  4. Null when nothing qualifies — callers leave the Contest-level
    ///     fields alone ("no line", never "push").
    /// </summary>
    public static class OddsProviderPreference
    {
        /// <summary>
        /// The set the display/scoring read-stack queries (see
        /// MatchupSqlBuilder + GetMatchup*.sql lateral joins). Order is the
        /// preference order. Widening this set REQUIRES widening the SQL on
        /// both the Producer and API sides in the same change.
        /// </summary>
        public static readonly string[] DisplayedProviderIds =
        {
            MatchupSqlBuilder.PreferredOddsProviderId.ToString(),   // 58 EspnBet
            MatchupSqlBuilder.FallbackOddsProviderId.ToString(),    // 100 DraftKings (2026 featured)
        };

        public static readonly string[] LiveOddsProviderIds =
        {
            SportsBook.EspnBetLiveOdds.ToProviderId(),     // 59
            SportsBook.DraftKingsLiveOdds.ToProviderId(),  // 200
        };

        public static CompetitionOdds? SelectPrimary(IEnumerable<CompetitionOdds>? allOdds)
        {
            if (allOdds is null) return null;

            var candidates = allOdds
                .Where(o => o.FinalizedUtc.HasValue && !LiveOddsProviderIds.Contains(o.ProviderId))
                .ToList();

            var displayed = candidates
                .Where(o => DisplayedProviderIds.Contains(o.ProviderId))
                .ToList();

            if (displayed.Count > 0)
            {
                // Modern era: resolve strictly within what the product reads.
                foreach (var id in DisplayedProviderIds)
                {
                    var withSpread = displayed.FirstOrDefault(o => o.ProviderId == id && o.Spread.HasValue);
                    if (withSpread != null) return withSpread;
                }
                foreach (var id in DisplayedProviderIds)
                {
                    var any = displayed.FirstOrDefault(o => o.ProviderId == id);
                    if (any != null) return any;
                }
            }

            // Historical era: no displayed-set rows at all.
            return candidates.FirstOrDefault(o => o.Spread.HasValue)
                   ?? candidates.FirstOrDefault();
        }
    }
}
