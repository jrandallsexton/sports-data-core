namespace SportsData.Api.Application.UI.Leagues.PickImport.Queries.GetPickImportPreview;

public sealed class GetPickImportPreviewQuery
{
    public required Guid UserId { get; init; }

    public required Guid SourceLeagueId { get; init; }

    public required Guid TargetLeagueId { get; init; }
}
