using SportsData.Core.Common;

namespace SportsData.Core.Infrastructure.Clients.Provider.Commands
{
    public class PublishDocumentEventsCommand
    {
        public Sport Sport { get; set; }

        public SourceDataProvider SourceDataProvider { get; set; }

        public DocumentType DocumentType { get; set; }
    }
}
