namespace SportsData.Api.Application.UI.Leagues.PickImport.Dtos;

/// <summary>
/// A candidate source league for importing picks into a target: one of the user's
/// other active same-type leagues that shares at least one contest with the target.
/// Backs the source picker. Returned by GET /ui/leagues/{targetId}/picks/import/sources.
/// </summary>
public sealed class PickImportSourceDto
{
    public Guid LeagueId { get; init; }

    public string Name { get; init; } = null!;

    public string Sport { get; init; } = null!;

    public string PickType { get; init; } = null!;

    public bool UseConfidencePoints { get; init; }

    /// <summary>Number of contests this source league shares with the target.</summary>
    public int SharedContestCount { get; init; }

    public int MemberCount { get; init; }
}
