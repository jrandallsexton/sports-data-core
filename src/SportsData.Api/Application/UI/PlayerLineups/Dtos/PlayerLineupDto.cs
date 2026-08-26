namespace SportsData.Api.Application.UI.PlayerLineups.Dtos;

public class PlayerLineupDto
{
    public Guid LeagueId { get; set; }
    public int SeasonYear { get; set; }
    public int SeasonWeek { get; set; }
    /// <summary>Filled slots only — the client knows the fixed shape.</summary>
    public List<PlayerLineupSlotDto> Slots { get; set; } = [];
}

public class PlayerLineupSlotDto
{
    public required string SlotId { get; set; }
    public Guid AthleteId { get; set; }
    public Guid AthleteSeasonId { get; set; }
    public required string Position { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string TeamName { get; set; }
    public required string TeamSlug { get; set; }
    /// <summary>Null = bye week (never locks, never scores, badge it).</summary>
    public Guid? ContestId { get; set; }
    public DateTime? ContestStartUtc { get; set; }
    public string? OpponentName { get; set; }
    /// <summary>Derived server-side at read time via the product-wide kickoff−5 rule.</summary>
    public bool IsLocked { get; set; }
}
