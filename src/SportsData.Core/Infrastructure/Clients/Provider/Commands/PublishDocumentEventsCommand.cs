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

        /// <summary>
        /// Optional batch size for processing documents. If not specified, defaults to 100.
        /// Reduce for large documents or limited memory. Increase for small documents with abundant memory.
        /// </summary>
        public int? BatchSize { get; set; } = 100;
    }
}
