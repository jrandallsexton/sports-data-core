namespace SportsData.Producer.Application.FranchiseContestFacts.Commands.ProjectContestFacts;

/// <summary>(Re)project the two team rows for one contest.</summary>
public record ProjectContestFactsCommand(Guid ContestId);
