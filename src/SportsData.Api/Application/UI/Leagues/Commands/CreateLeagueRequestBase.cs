namespace SportsData.Api.Application.UI.Leagues.Commands;

/// <summary>
/// Shared fields for all sport-specific create-league requests.
/// </summary>
public abstract class CreateLeagueRequestBase
{
    public required string Name { get; set; }

    public string? Description { get; set; }

    public required string PickType { get; set; }

    public bool UseConfidencePoints { get; set; }

    public required string TiebreakerType { get; set; }

    public required string TiebreakerTiePolicy { get; set; }

    public bool IsPublic { get; set; }

    public int? DropLowWeeksCount { get; set; }

    /// <summary>
    /// The season year. Defaults to the current year if not specified.
    /// </summary>
    public int? SeasonYear { get; set; }

    /// <summary>
    /// Inclusive start of the league window. Null = from the start of the season.
    /// </summary>
    public DateTime? StartsOn { get; set; }

    /// <summary>
    /// Inclusive end of the league window. Null = through the end of the season.
    /// </summary>
    public DateTime? EndsOn { get; set; }
}
