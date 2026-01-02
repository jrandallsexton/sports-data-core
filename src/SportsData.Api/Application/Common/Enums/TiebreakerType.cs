namespace SportsData.Api.Application.Common.Enums;

public enum TiebreakerType
{
    None = 0,
    TotalPoints = 1,       // Guess the total combined score
    HomeAndAwayScores = 2, // Guess each team's score
    EarliestSubmission = 3 // Winner determined by earliest pick submission
}

public enum TiebreakerTiePolicy
{
    EarliestSubmission = 0
    // Random = 1,
    // SharedWin = 2,
    // CommissionerDecision = 3
}
