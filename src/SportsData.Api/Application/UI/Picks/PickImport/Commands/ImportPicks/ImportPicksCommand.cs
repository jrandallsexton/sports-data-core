namespace SportsData.Api.Application.UI.Picks.PickImport.Commands.ImportPicks;

public sealed class ImportPicksCommand
{
    public required Guid UserId { get; init; }

    public required Guid SourceLeagueId { get; init; }

    public required Guid TargetLeagueId { get; init; }

    /// <summary>
    /// The contests the user selected to import (checked in the dialog). Only these
    /// are imported/replaced; every other contest is left untouched. Contest ids
    /// that aren't importable per the server-side plan are ignored.
    /// </summary>
    public IReadOnlyCollection<Guid> ContestIds { get; init; } = [];
}
