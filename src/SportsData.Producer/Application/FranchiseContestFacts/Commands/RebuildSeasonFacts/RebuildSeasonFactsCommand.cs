namespace SportsData.Producer.Application.FranchiseContestFacts.Commands.RebuildSeasonFacts;

/// <summary>Re-project every contest of one season (backfill and re-projection).</summary>
public record RebuildSeasonFactsCommand(int SeasonYear);
