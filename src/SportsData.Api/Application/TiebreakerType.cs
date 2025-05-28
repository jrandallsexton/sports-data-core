namespace SportsData.Api.Application
{
    public enum TiebreakerType
    {
        None = 0,
        TotalPoints = 1,       // Guess the total combined score
        HomeAndAwayScores = 2  // Guess each team's score
    }

    public enum TiebreakerTiePolicy
    {
        EarliestSubmission = 0
        // Random = 1,
        // SharedWin = 2,
        // CommissionerDecision = 3
    }
}
