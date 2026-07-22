using SportsData.Core.Common;

namespace SportsData.Producer.Application.Contests.Commands;

/// <summary>
/// Re-source every contest for a (sport, season) through the narrowed
/// "Refresh Contest" path (<see cref="UpdateContestCommand"/> →
/// ContestUpdateProcessor → EventCompetition + record/play documents, no
/// athletes/rosters). The season → distinct-ContestId set is derived from the
/// FranchiseSeason traversal rather than the Contest.SeasonYear denormalization.
/// See docs/features/season-contest-resource-driver.md.
/// </summary>
public record RefreshContestsBySeasonYearCommand
{
    public Sport Sport { get; init; }

    public int SeasonYear { get; init; }

    public Guid CorrelationId { get; init; } = Guid.Empty;
}
