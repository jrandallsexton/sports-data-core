namespace SportsData.Api.Application.UI.Picks.PickImport.Dtos;

/// <summary>
/// Body for POST /ui/leagues/{targetId}/picks/import. The target league is the
/// route id; this names the source league and the collisions the user chose to
/// replace (by contest id). Contests not listed here keep their existing target
/// pick.
/// </summary>
public sealed class PickImportRequest
{
    public Guid SourceLeagueId { get; set; }

    public List<Guid> ReplaceContestIds { get; set; } = [];
}
