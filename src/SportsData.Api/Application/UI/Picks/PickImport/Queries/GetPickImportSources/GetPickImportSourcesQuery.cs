namespace SportsData.Api.Application.UI.Picks.PickImport.Queries.GetPickImportSources;

public sealed class GetPickImportSourcesQuery
{
    public required Guid UserId { get; init; }

    public required Guid TargetLeagueId { get; init; }
}
