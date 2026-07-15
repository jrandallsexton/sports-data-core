namespace SportsData.Api.Application.UI.Picks.PickImport.Commands.ImportPicks;

public sealed class ImportPicksCommand
{
    public required Guid UserId { get; init; }

    public required Guid SourceLeagueId { get; init; }

    public required Guid TargetLeagueId { get; init; }

    /// <summary>Contest ids for collisions the user chose to replace with the source pick.</summary>
    public IReadOnlyCollection<Guid> ReplaceContestIds { get; init; } = [];
}
