using SportsData.Api.Infrastructure.Data.Entities;

namespace SportsData.Api.Application.Scoring;

/// <summary>
/// A matchup's per-team American prices, as stored on PickemGroupMatchup.
/// Scoring reads them at scoring time, so they are the closing prices.
/// </summary>
public record MatchupPricing(
    int? AwayMoneyLine,
    int? HomeMoneyLine,
    double? AwaySpreadPrice,
    double? HomeSpreadPrice)
{
    public static MatchupPricing From(PickemGroupMatchup matchup) =>
        new(matchup.AwayMoneyLine, matchup.HomeMoneyLine, matchup.AwaySpreadPrice, matchup.HomeSpreadPrice);
}
