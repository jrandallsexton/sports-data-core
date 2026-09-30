using System;

namespace SportsData.Core.Dtos.Canonical
{
    /// <summary>
    /// Current odds pricing for one contest: each team's moneyline and spread
    /// price, and the over/under prices. Taken from the same odds provider row
    /// as the league matchup queries (preferred provider, else fallback), so the
    /// prices pair with the spread and total a PickemGroupMatchup already holds.
    /// </summary>
    /// <remarks>
    /// Moneyline and spread price are per team and are not derivable from one
    /// another (unlike the spread, whose away value is the negated home value),
    /// so both sides are carried. Every price is null when no odds exist from
    /// either provider.
    /// </remarks>
    public record OddsPricingDto
    {
        public Guid ContestId { get; init; }

        /// <summary>American moneyline, e.g. +240.</summary>
        public int? AwayMoneyLine { get; init; }

        /// <summary>American moneyline, e.g. -300.</summary>
        public int? HomeMoneyLine { get; init; }

        /// <summary>Price on the away spread, e.g. -110.</summary>
        public decimal? AwaySpreadPrice { get; init; }

        /// <summary>Price on the home spread, e.g. -110.</summary>
        public decimal? HomeSpreadPrice { get; init; }

        public decimal? OverOdds { get; init; }

        public decimal? UnderOdds { get; init; }
    }
}
