namespace SportsData.Core.Eventing.Events.Contests
{
    /// <summary>
    /// Snapshot of the DISPLAYED odds row for a contest: the row the league
    /// matchup cards read (Producer's OddsProviderPreference.DisplayedProviderIds,
    /// first-existing wins: ESPN Bet, else DraftKings). Carried on
    /// <see cref="ContestOddsCreated"/> / <see cref="ContestOddsUpdated"/> only
    /// when the event's odds ARE that row, so a consumer applying it to
    /// PickemGroupMatchup never mixes in a different book's line or prices.
    /// </summary>
    /// <remarks>
    /// <see cref="Spread"/> is home-relative, as on the matchup (away = -home).
    /// Moneyline and spread price are per team: unlike the spread, neither is
    /// derivable from the other side.
    /// </remarks>
    public record DisplayedContestOdds
    {
        public string ProviderId { get; init; } = default!;

        /// <summary>ESPN's line text, e.g. "ARI -3.5" (PickemGroupMatchup.Spread).</summary>
        public string? Details { get; init; }

        /// <summary>Home-relative spread; negative = home favored.</summary>
        public decimal? Spread { get; init; }

        public decimal? OverUnder { get; init; }

        public decimal? OverOdds { get; init; }

        public decimal? UnderOdds { get; init; }

        public int? AwayMoneyLine { get; init; }

        public int? HomeMoneyLine { get; init; }

        public decimal? AwaySpreadPrice { get; init; }

        public decimal? HomeSpreadPrice { get; init; }
    }
}
