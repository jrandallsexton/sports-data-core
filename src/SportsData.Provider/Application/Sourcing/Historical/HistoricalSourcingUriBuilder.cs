using Microsoft.Extensions.Options;
using SportsData.Core.Common;

namespace SportsData.Provider.Application.Sourcing.Historical;

public interface IHistoricalSourcingUriBuilder
{
    /// <summary>
    /// Builds the ESPN API URI for a specific document type and season.
    /// </summary>
    Uri BuildUri(DocumentType documentType, int seasonYear, Sport sport, SourceDataProvider provider);
}

public class HistoricalSourcingUriBuilder : IHistoricalSourcingUriBuilder
{
    private readonly HistoricalSourcingConfig _config;

    private static readonly Dictionary<Sport, string> EspnLeagueMap = new()
    {
        { Sport.FootballNcaa, "college-football" },
        { Sport.FootballNfl, "nfl" }
    };

    public HistoricalSourcingUriBuilder(IOptions<HistoricalSourcingConfig> config)
    {
        _config = config.Value;
    }

    public Uri BuildUri(DocumentType documentType, int seasonYear, Sport sport, SourceDataProvider provider)
    {
        if (provider != SourceDataProvider.Espn || !EspnLeagueMap.ContainsKey(sport))
        {
            throw new NotSupportedException(
                $"Historical sourcing not yet supported for {sport}/{provider}");
        }

        return BuildEspnUri(documentType, seasonYear, sport);
    }

    private Uri BuildEspnUri(DocumentType documentType, int seasonYear, Sport sport)
    {
        var league = EspnLeagueMap[sport];
        var baseUrl = $"{_config.EspnBaseUrl.TrimEnd('/')}/{league}";

        var path = documentType switch
        {
            DocumentType.Season => $"{baseUrl}/seasons/{seasonYear}",

            // ESPN venues endpoint is league-level (not season-specific), so seasonYear is intentionally unused
            DocumentType.Venue => $"{baseUrl}/venues",

            DocumentType.TeamSeason => $"{baseUrl}/seasons/{seasonYear}/teams",
            DocumentType.AthleteSeason => $"{baseUrl}/seasons/{seasonYear}/athletes",
            _ => throw new ArgumentException(
                $"Document type {documentType} is not supported for historical sourcing")
        };

        return new Uri(path);
    }
}
