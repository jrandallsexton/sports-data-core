using SportsData.Core.Common;

namespace SportsData.Core.Infrastructure.Clients.Provider.Commands
{
    public class PublishDocumentEventsCommand
    {
        public Sport Sport { get; set; }

        public SourceDataProvider SourceDataProvider { get; set; }

        public DocumentType DocumentType { get; set; }

        public int? Season { get; set; }
    }

    public class GetExternalDocumentQuery(
        string canonicalId,
        string url,
        SourceDataProvider sourceDataProvider,
        Sport sport,
        DocumentType documentType,
        int? seasonYear)
    {

        public string CanonicalId { get; init; } = canonicalId;

        public string Url { get; init; } = url;

        public SourceDataProvider SourceDataProvider { get; init; } = sourceDataProvider;

        public Sport Sport { get; init; } = sport;

        public DocumentType DocumentType { get; init; } = documentType;

        public int? SeasonYear { get; init; } = seasonYear;
    }

    public class GetExternalDocumentQueryResponse
    {
        public string Id { get; set; }

        public string CanonicalId { get; set; }

        public string Href { get; set; }
    }

    public class ProcessResourceIndexCommand
    {
        public SourceDataProvider SourceDataProvider { get; set; }

        public Sport Sport { get; set; }

        public DocumentType DocumentType { get; set; }

        public int? Season { get; set; }

        public string ResourceIndexUrl { get; set; }
    }
}
