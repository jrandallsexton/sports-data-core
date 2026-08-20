namespace SportsData.Api.Application.UI.Matchups;

public class MatchupPreviewValidator
{
    public record ValidationResult(bool IsValid, List<string> Errors);

    /// <summary>Matches MatchupPreviewPrompt.ResponseValidationErrors' column cap.</summary>
    public const int MaxErrorTextLength = 1024;

    /// <summary>
    /// Compose attempt-1 and retry diagnostics into one capture-column value
    /// under the 1024-char cap, guaranteeing the RETRY section always
    /// survives truncation: the retry gets first claim on up to half the
    /// budget (more when attempt-1 is short), and each section is truncated
    /// only within its own share. Retry null = attempt-1 only.
    /// </summary>
    public static string ComposeErrorSections(string attempt1, string? retry)
    {
        var attemptSection = $"Attempt 1: {attempt1}";

        if (retry is null)
        {
            return attemptSection.Length <= MaxErrorTextLength
                ? attemptSection
                : attemptSection[..MaxErrorTextLength];
        }

        const string separator = " | ";
        var retrySection = $"Retry: {retry}";

        if (attemptSection.Length + separator.Length + retrySection.Length <= MaxErrorTextLength)
            return attemptSection + separator + retrySection;

        var retryBudget = Math.Min(
            retrySection.Length,
            Math.Max(MaxErrorTextLength / 2, MaxErrorTextLength - separator.Length - attemptSection.Length));
        var attemptBudget = MaxErrorTextLength - separator.Length - retryBudget;

        if (attemptSection.Length > attemptBudget) attemptSection = attemptSection[..attemptBudget];
        if (retrySection.Length > retryBudget) retrySection = retrySection[..retryBudget];

        return attemptSection + separator + retrySection;
    }

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

        if (homeScore < 0 || awayScore < 0)
        {
            errors.Add("Scores cannot be negative.");
        }

        if (predictedStraightUpWinner == Guid.Empty)
        {
            errors.Add("Straight-up winner is not set (Guid.Empty).");
        }

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
        }

        if (Math.Abs(spread) < 0.1)
        {
            if (predictedSpreadWinner.HasValue)
            {
                errors.Add("Spread is zero (pick'em), but a spread winner was predicted.");
            }
        }
        else if (Math.Abs(actualMargin - Math.Abs(spread)) < 0.1)
        {
            if (predictedSpreadWinner.HasValue)
            {
                errors.Add("Spread prediction should be null (push), but a winner was predicted.");
            }
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

        if (predictedStraightUpWinner != homeFranchiseSeasonId &&
            predictedStraightUpWinner != awayFranchiseSeasonId)
        {
            errors.Add("Straight-up winner is not one of the valid team FranchiseSeasonIds.");
        }

        if (predictedSpreadWinner.HasValue &&
            predictedSpreadWinner.Value != homeFranchiseSeasonId &&
            predictedSpreadWinner.Value != awayFranchiseSeasonId)
        {
            errors.Add("Spread winner is not one of the valid team FranchiseSeasonIds.");
        }

        var totalScore = homeScore + awayScore;
        if (totalScore > 120)
        {
            errors.Add($"Total score ({totalScore}) appears unreasonably high.");
        }

        return new ValidationResult(errors.Count == 0, errors);
    }
}