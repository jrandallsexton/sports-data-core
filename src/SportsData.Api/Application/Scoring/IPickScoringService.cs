using SportsData.Core.Dtos.Canonical;
using SportsData.Api.Infrastructure.Data.Entities;

namespace SportsData.Api.Application.Scoring;

public interface IPickScoringService
{
    void ScorePick(
        PickemGroup group,
        double? spread,
        PickemGroupUserPick pick,
        MatchupResult result);

    /// <summary>
    /// Writes the simulated $1 bet columns (PointsSU, PointsATS, PointsOU)
    /// from the league's pick type, the result and the matchup's closing
    /// prices. Touches nothing else on the pick, so the pricing backfill can
    /// run it on already-scored picks without moving IsCorrect/PointsAwarded.
    /// </summary>
    void ScoreSimulatedBets(
        PickemGroup group,
        double? spread,
        PickemGroupUserPick pick,
        MatchupResult result,
        MatchupPricing? pricing);
}