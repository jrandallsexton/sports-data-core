namespace SportsData.Provider.Application.Sourcing.Historical;

/// <summary>
/// Configuration for historical season sourcing tier delays.
/// </summary>
public class HistoricalSourcingConfig
{
    public const string SectionName = "HistoricalSourcing";

    /// <summary>
    /// Base URL for ESPN API (e.g. https://sports.core.api.espn.com/v2/sports/football/leagues/college-football)
    /// </summary>
    public string EspnBaseUrl { get; set; } = "https://sports.core.api.espn.com/v2/sports/football/leagues/college-football";

    /// <summary>
    /// Default tier delays in minutes for each sport/provider combination.
    /// </summary>
    public Dictionary<string, Dictionary<string, TierDelays>> DefaultTierDelays { get; set; } = new();
}

/// <summary>
/// Tier delay configuration in minutes.
/// </summary>
public class TierDelays
{
    /// <summary>
    /// Delay before processing Season (typically 0)
    /// </summary>
    public int Season { get; set; }

    /// <summary>
    /// Delay before processing Venues
    /// </summary>
    public int Venue { get; set; }

    /// <summary>
    /// Delay before processing TeamSeasons
    /// </summary>
    public int TeamSeason { get; set; }

    /// <summary>
    /// Delay before processing AthleteSeasons
    /// </summary>
    public int AthleteSeason { get; set; }
}
