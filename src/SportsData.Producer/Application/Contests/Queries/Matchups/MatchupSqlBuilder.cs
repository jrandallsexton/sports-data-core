using SportsData.Producer.Enums;

namespace SportsData.Producer.Application.Contests.Queries.Matchups;

/// <summary>
/// Odds provider ID constants used by SQL queries.
/// SQL fragments have been extracted to embedded .sql resource files
/// loaded by <see cref="Infrastructure.Sql.ProducerSqlQueryProvider"/>.
/// </summary>
public static class MatchupSqlBuilder
{
    /// <summary>
    /// Preferred odds provider (ESPN Bet). Fallback is DraftKings.
    /// These map to SportsBook enum values stored in CompetitionOdds.ProviderId.
    /// </summary>
    public const int PreferredOddsProviderId = (int)SportsBook.EspnBet;       // 58
    public const int FallbackOddsProviderId = (int)SportsBook.DraftKings100;   // 100
}
