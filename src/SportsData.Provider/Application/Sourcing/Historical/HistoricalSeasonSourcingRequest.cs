using SportsData.Core.Common;

namespace SportsData.Provider.Application.Sourcing.Historical;

/// <summary>
/// Request to source a complete historical season from ESPN.
/// </summary>
public record HistoricalSeasonSourcingRequest
{
    /// <summary>
    /// Sport to source (e.g., FootballNcaa)
    /// </summary>
    public required Sport Sport { get; init; }

    /// <summary>
    /// Data provider (e.g., Espn)
    /// </summary>
    public required SourceDataProvider SourceDataProvider { get; init; }

    /// <summary>
    /// Year of the season to source (e.g., 2024)
    /// </summary>
    public required int SeasonYear { get; init; }

    /// <summary>
    /// Optional tier delays in minutes. If omitted, uses configured defaults.
    /// Keys: "season", "venue", "teamSeason", "athleteSeason"
    /// </summary>
    public Dictionary<string, int>? TierDelays { get; init; }
}
