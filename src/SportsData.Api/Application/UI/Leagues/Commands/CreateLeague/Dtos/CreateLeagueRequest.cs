namespace SportsData.Api.Application.UI.Leagues.Commands.CreateLeague.Dtos;

public class CreateLeagueRequest
{
    public required string Name { get; set; }

    public string? Description { get; set; }

    public required string PickType { get; set; } // "straight" or "spread" from UI

    public bool UseConfidencePoints { get; set; }

    public required string TiebreakerType { get; set; } // "none", "totalPoints"
    public required string TiebreakerTiePolicy { get; set; } // "earliest", etc.

    public string? RankingFilter { get; set; } // "AP_TOP_25", etc.

    public List<string> ConferenceSlugs { get; set; } = []; // slugs from UI

    public bool IsPublic { get; set; }

    public int? DropLowWeeksCount { get; set; }

    /// <summary>
    /// The season year for conference resolution. Defaults to current year if not specified.
    /// For college football, this is the year the season starts (e.g., 2025 for the 2025-26 season).
    /// </summary>
    public int? SeasonYear { get; set; }
}