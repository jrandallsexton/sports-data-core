using SportsData.Core.Common;

using System.Collections.Generic;

namespace SportsData.Core.Infrastructure.Clients.Provider.Commands
{
    public class PublishDocumentEventsCommand
    {
        public Sport Sport { get; set; }

        public SourceDataProvider SourceDataProvider { get; set; }

        public DocumentType DocumentType { get; set; }

        public int? Season { get; set; }

        public List<DocumentType>? IncludeLinkedDocumentTypes { get; set; } = [];
    }
}
