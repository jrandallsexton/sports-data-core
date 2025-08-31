using SportsData.Api.Infrastructure.Data.Canonical.Models;
using SportsData.Api.Infrastructure.Data.Entities;

namespace SportsData.Api.Application.Scoring;

public class PickScoringService : IPickScoringService
{
    public void ScorePick(PickemGroup group, PickemGroupUserPick pick, MatchupResult result)
    {
        var now = DateTime.UtcNow;

        switch (group.PickType)
        {
            case PickType.None:
            case PickType.StraightUp:
                ScoreStraightUp(pick, result, now);
                pick.WasAgainstSpread = false;
                break;

            case PickType.AgainstTheSpread:
                ScoreAgainstSpread(pick, result, now);
                pick.WasAgainstSpread = true;
                break;

            case PickType.OverUnder:
                // TODO: Implement when OverUnder scoring logic is ready
                break;

            default:
                throw new InvalidOperationException("Unsupported PickType: " + group.PickType);
        }

        if (group.UseConfidencePoints)
        {
            // TODO: Apply confidence logic to pick.PointsAwarded
        }
    }

    private void ScoreStraightUp(PickemGroupUserPick pick, MatchupResult result, DateTime now)
    {
        if (!pick.FranchiseId.HasValue)
        {
            SetIncorrect(pick, now);
            return;
        }

        pick.IsCorrect = pick.FranchiseId == result.WinnerFranchiseSeasonId;
        pick.ScoredAt = now;
        pick.PointsAwarded = pick.IsCorrect.Value ? 1 : 0;
    }

    private void ScoreAgainstSpread(PickemGroupUserPick pick, MatchupResult result, DateTime now)
    {
        if (!pick.FranchiseId.HasValue)
        {
            SetIncorrect(pick, now);
            return;
        }

        var spreadWinner = result.SpreadWinnerFranchiseSeasonId;

        if (spreadWinner.HasValue)
        {
            pick.IsCorrect = pick.FranchiseId == spreadWinner.Value;
        }
        else
        {
            pick.IsCorrect = pick.FranchiseId == result.WinnerFranchiseSeasonId;
        }

        pick.ScoredAt = now;
        pick.PointsAwarded = pick.IsCorrect.Value ? 1 : 0;
    }

    private void SetIncorrect(PickemGroupUserPick pick, DateTime now)
    {
        pick.IsCorrect = false;
        pick.ScoredAt = now;
        pick.PointsAwarded = 0;
    }
}