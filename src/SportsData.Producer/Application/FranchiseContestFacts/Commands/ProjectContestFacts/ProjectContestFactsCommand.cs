namespace SportsData.Producer.Application.FranchiseContestFacts.Commands.ProjectContestFacts;

/// <summary>(Re)project the two team rows for one contest.</summary>
/// <param name="ContestId">The contest to project.</param>
/// <param name="SeasonUsesConferenceFlag">
/// Precomputed season-wide switch (<see cref="SeasonConferenceFlag"/>). A
/// season rebuild passes it so thousands of jobs do not each recount the
/// season; null (the per-game triggers) computes it.
/// </param>
public record ProjectContestFactsCommand(Guid ContestId, bool? SeasonUsesConferenceFlag = null);
