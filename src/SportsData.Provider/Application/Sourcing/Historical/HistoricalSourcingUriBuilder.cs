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
    public Uri BuildUri(DocumentType documentType, int seasonYear, Sport sport, SourceDataProvider provider)
    {
        if (sport == Sport.FootballNcaa && provider == SourceDataProvider.Espn)
        {
            return BuildEspnFootballNcaaUri(documentType, seasonYear);
        }

        throw new NotSupportedException(
            $"Historical sourcing not yet supported for {sport}/{provider}");
    }

    private static Uri BuildEspnFootballNcaaUri(DocumentType documentType, int seasonYear)
    {
        const string baseUrl = "https://sports.core.api.espn.com/v2/sports/football/leagues/college-football";

        var path = documentType switch
        {
            DocumentType.Season => $"{baseUrl}/seasons/{seasonYear}",
            DocumentType.Venue => $"{baseUrl}/venues",
            DocumentType.TeamSeason => $"{baseUrl}/seasons/{seasonYear}/teams",
            DocumentType.AthleteSeason => $"{baseUrl}/seasons/{seasonYear}/athletes",
            _ => throw new ArgumentException(
                $"Document type {documentType} is not supported for historical sourcing")
        };

        return new Uri(path);
    }
}
