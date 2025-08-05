using SportsData.Api.Infrastructure.Data.Entities;

namespace SportsData.Api.Application.Scoring
{
    public interface IPickScoringService
    {
        PickemGroupUserPick ScorePick(PickemGroupUserPick pick, Contest contest, Infrastructure.Data.Entities.PickemGroup league);
    }

    public class PickScoringService : IPickScoringService
    {
        public PickemGroupUserPick ScorePick(PickemGroupUserPick pick, Contest contest, Infrastructure.Data.Entities.PickemGroup league)
        {
            if (contest == null || contest.ContestId != pick.ContestId)
                throw new ArgumentException("Invalid or mismatched contest.");

            if (!contest.IsFinal)
                throw new InvalidOperationException("Contest has not been finalized.");

            var isCorrect = false;
            var points = 0;

            var leaguePickTypes = league.PickType;

            // Straight-Up
            if (leaguePickTypes.HasFlag(PickType.StraightUp) && pick.FranchiseId.HasValue)
            {
                if (contest.WinnerFranchiseId.HasValue && pick.FranchiseId == contest.WinnerFranchiseId)
                {
                    isCorrect = true;
                    points += 1;
                }
            }

            // Against the Spread
            if (leaguePickTypes.HasFlag(PickType.AgainstTheSpread) && pick.FranchiseId.HasValue)
            {
                pick.WasAgainstSpread = true;

                if (contest.SpreadWinnerFranchiseId.HasValue && pick.FranchiseId == contest.SpreadWinnerFranchiseId)
                {
                    isCorrect = true;
                    points += 1;
                }
            }

            // Over/Under
            if (leaguePickTypes.HasFlag(PickType.OverUnder) &&
                pick.OverUnder.HasValue &&
                contest.OverUnder.HasValue &&
                contest.HomeScore.HasValue &&
                contest.AwayScore.HasValue)
            {
                var totalScore = contest.HomeScore.Value + contest.AwayScore.Value;
                var wentOver = totalScore > contest.OverUnder.Value;
                var wentUnder = totalScore < contest.OverUnder.Value;

                if ((wentOver && pick.OverUnder == OverUnderPick.Over) ||
                    (wentUnder && pick.OverUnder == OverUnderPick.Under))
                {
                    isCorrect = true;
                    points += 1;
                }
            }

            // Confidence Scoring (overrides point value if correct)
            if (league.UseConfidencePoints &&
                pick.ConfidencePoints.HasValue &&
                isCorrect)
            {
                points = pick.ConfidencePoints.Value;
            }

            pick.IsCorrect = isCorrect;
            pick.PointsAwarded = points;
            pick.ScoredAt = DateTime.UtcNow;

            return pick;
        }
    }
}
