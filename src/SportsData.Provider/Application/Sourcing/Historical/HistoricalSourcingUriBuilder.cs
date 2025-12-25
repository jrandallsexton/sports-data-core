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

    public HistoricalSourcingUriBuilder(IOptions<HistoricalSourcingConfig> config)
    {
        _config = config.Value;
    }

    public Uri BuildUri(DocumentType documentType, int seasonYear, Sport sport, SourceDataProvider provider)
    {
        if (sport == Sport.FootballNcaa && provider == SourceDataProvider.Espn)
        {
            return BuildEspnFootballNcaaUri(documentType, seasonYear);
        }

        throw new NotSupportedException(
            $"Historical sourcing not yet supported for {sport}/{provider}");
    }

    private Uri BuildEspnFootballNcaaUri(DocumentType documentType, int seasonYear)
    {
        var baseUrl = _config.EspnBaseUrl.TrimEnd('/');

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
