namespace SportsData.Api.Application.UI.PlayerLineups.Commands.UpsertLineupSlot;

/// <summary>
/// Assign or replace one slot. Contest anchoring (ContestId/StartUtc) is
/// deliberately ABSENT here: the server resolves it from its own matchup
/// data — client-provided contest fields are never trusted for locking.
/// OpponentName is display-only and accepted from the client.
/// </summary>
public class UpsertLineupSlotCommand
{
    public Guid LeagueId { get; set; }
    public Guid UserId { get; set; }
    public int SeasonYear { get; set; }
    public int SeasonWeek { get; set; }
    public string SlotId { get; set; } = null!;

    public Guid AthleteId { get; set; }
    public Guid AthleteSeasonId { get; set; }
    public string Position { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string TeamName { get; set; } = null!;
    public string TeamSlug { get; set; } = null!;
    public string? OpponentName { get; set; }
}
