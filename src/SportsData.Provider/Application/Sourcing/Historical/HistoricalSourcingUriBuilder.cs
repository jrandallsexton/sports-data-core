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
        if (provider != SourceDataProvider.Espn)
        {
            throw new NotSupportedException(
                $"Historical sourcing not yet supported for provider {provider}");
        }

        var sportKey = sport.ToString();
        if (!_config.EspnBaseUrls.TryGetValue(sportKey, out var baseUrl) || string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new NotSupportedException(
                $"No ESPN base URL configured for {sport}. Add HistoricalSourcing:EspnBaseUrls:{sportKey} to App Config.");
        }

        return BuildEspnUri(documentType, seasonYear, baseUrl.TrimEnd('/'));
    }

    private static Uri BuildEspnUri(DocumentType documentType, int seasonYear, string baseUrl)
    {
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
