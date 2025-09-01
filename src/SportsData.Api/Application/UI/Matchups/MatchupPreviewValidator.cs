namespace SportsData.Api.Application.UI.Matchups;

public class MatchupPreviewValidator
{
    public record ValidationResult(bool IsValid, List<string> Errors);

    public static ValidationResult Validate(
        Guid contestId,
        int homeScore,
        int awayScore,
        double homeSpread,
        Guid predictedStraightUpWinner,
        Guid? predictedSpreadWinner,
        Guid homeFranchiseSeasonId,
        Guid awayFranchiseSeasonId)
    {
        var errors = new List<string>();

        // 1. Straight-Up Winner Check
        if (awayScore > homeScore && predictedStraightUpWinner != awayFranchiseSeasonId)
        {
            errors.Add("Straight-up winner is incorrect. Away team scored more but prediction points to home.");
        }
        else if (homeScore > awayScore && predictedStraightUpWinner != homeFranchiseSeasonId)
        {
            errors.Add("Straight-up winner is incorrect. Home team scored more but prediction points to away.");
        }
        else if (homeScore == awayScore)
        {
            errors.Add("Straight-up winner is incorrect. Game is a tie but a winner was predicted.");
        }

        // 2. Spread Winner Check (final corrected logic)
        var actualMargin = homeScore - awayScore;
        var spread = homeSpread;

        // A "push" means the favorite won by exactly the spread
        if (Math.Abs(actualMargin - Math.Abs(spread)) < 0.1)
        {
            if (predictedSpreadWinner.HasValue)
            {
                errors.Add("Spread prediction should be null (push), but a winner was predicted.");
            }
            return new ValidationResult(errors.Count == 0, errors);
        }
        else
        {
            Guid expectedWinner;

            if (spread < 0) // Home is favored
                expectedWinner = actualMargin > Math.Abs(spread) ? homeFranchiseSeasonId : awayFranchiseSeasonId;
            else // Away is favored
                expectedWinner = -actualMargin > Math.Abs(spread) ? awayFranchiseSeasonId : homeFranchiseSeasonId;

            if (!predictedSpreadWinner.HasValue || predictedSpreadWinner.Value != expectedWinner)
            {
                errors.Add("Spread winner is inconsistent with spread and score differential.");
            }
        }

        // 3. Ensure winner ids not set to ContestId (yes, i've seen this)
        if (contestId == predictedStraightUpWinner)
        {
            errors.Add("Straight-up winner's FranchiseSeasonId is the ContestId.");
        }

        if (contestId == predictedSpreadWinner)
        {
            errors.Add("Spread winner's FranchiseSeasonId is the ContestId.");
        }

        return new ValidationResult(errors.Count == 0, errors);
    }
}