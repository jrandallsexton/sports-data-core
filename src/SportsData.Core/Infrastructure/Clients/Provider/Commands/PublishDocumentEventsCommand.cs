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

    public class GetExternalDocumentQuery
    {
        public string Url { get; set; }

        public SourceDataProvider SourceDataProvider { get; set; }

        public Sport Sport { get; set; }

        public DocumentType DocumentType { get; set; }

        public int? SeasonYear { get; set; }
    }

    public class GetExternalDocumentQueryResponse
    {
        public string Href { get; set; }
    }
}
