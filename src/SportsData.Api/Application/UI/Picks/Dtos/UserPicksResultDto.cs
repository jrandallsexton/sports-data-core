namespace SportsData.Api.Application.UI.Picks.Dtos;

/// <summary>
/// Envelope for GET /ui/picks/{groupId}/week/{week}: the current user's picks
/// plus server-computed result counts driving the ended-league header glance
/// (X|Y|Z). X (no scored pick: unpicked + never-resolved games) is derived
/// client-side as <c>TotalMatchups - CorrectCount - IncorrectCount</c> — the
/// server sends the minimal orthogonal set so a redundant fourth counter can't
/// drift out of agreement. See docs/features/league-ended-headers.md.
/// </summary>
public record UserPicksResultDto
{
    public List<UserPickDto> Picks { get; init; } = [];

    /// <summary>
    /// Total matchups in this group-week (from PickemGroupMatchup), regardless
    /// of whether the user picked them.
    /// </summary>
    public int TotalMatchups { get; init; }

    /// <summary>Picks with <c>IsCorrect == true</c>.</summary>
    public int CorrectCount { get; init; }

    /// <summary>Picks with <c>IsCorrect == false</c>.</summary>
    public int IncorrectCount { get; init; }
}
