using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.Documents
{
    public class DocumentUpdated : EventBase
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public DocumentType DocumentType { get; set; }

        public SourceDataProvider SourceDataProvider { get; set; }
    }
}
