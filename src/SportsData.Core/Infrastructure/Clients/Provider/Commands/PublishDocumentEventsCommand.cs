using SportsData.Core.Common;

namespace SportsData.Core.Infrastructure.Clients.Provider.Commands
{
    public class PublishDocumentEventsCommand
    {
        public SourceDataProvider SourceDataProvider { get; set; }

        public DocumentType DocumentType { get; set; }
    }
}
