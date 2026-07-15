namespace SportsData.Api.Application.UI.Picks.PickImport.Dtos;

/// <summary>
/// Body for POST /ui/leagues/{targetId}/picks/import/preview. The target league is
/// the route id; this names the league to copy the user's picks from.
/// </summary>
public sealed class PickImportPreviewRequest
{
    public Guid SourceLeagueId { get; set; }
}
