using SportsData.Core.Common;

using System;

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
        Uri uri,
        SourceDataProvider sourceDataProvider,
        Sport sport,
        DocumentType documentType,
        int? seasonYear)
    {

        public string CanonicalId { get; init; } = canonicalId;

        public Uri Uri { get; init; } = uri;

        public SourceDataProvider SourceDataProvider { get; init; } = sourceDataProvider;

        public Sport Sport { get; init; } = sport;

        public DocumentType DocumentType { get; init; } = documentType;

        public int? SeasonYear { get; init; } = seasonYear;
    }

    public class GetExternalDocumentQueryResponse
    {
        public required string Id { get; set; }

        public required string CanonicalId { get; set; }

        public required Uri Uri { get; set; }

        public bool IsSuccess { get; set; } = true;
    }

    public class ProcessResourceIndexCommand
    {
        public SourceDataProvider SourceDataProvider { get; set; }

        public Sport Sport { get; set; }

        public DocumentType DocumentType { get; set; }

        public int? Season { get; set; }

        public required string ResourceIndexUrl { get; set; }
    }
}
