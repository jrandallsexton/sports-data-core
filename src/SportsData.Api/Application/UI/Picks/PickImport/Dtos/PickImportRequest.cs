namespace SportsData.Api.Application.UI.Picks.PickImport.Dtos;

/// <summary>
/// Body for POST /ui/leagues/{targetId}/picks/import. The target league is the
/// route id; this names the source league and the contests the user selected to
/// import (the checked boxes). Only those are imported; every other contest is
/// left untouched.
/// </summary>
public sealed class PickImportRequest
{
    public Guid SourceLeagueId { get; set; }

    public List<Guid> ContestIds { get; set; } = [];
}
