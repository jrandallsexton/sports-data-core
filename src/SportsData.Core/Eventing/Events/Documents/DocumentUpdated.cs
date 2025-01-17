using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.Documents
{
    public class DocumentUpdated(
        string id,
        string name,
        Sport sport,
        DocumentType documentType,
        SourceDataProvider sourceDataProvider)
        : EventBase
    {
        public string Id { get; init; } = id;

        public string Name { get; init; } = name;

        public Sport Sport { get; init; } = sport;

        public DocumentType DocumentType { get; init; } = documentType;

        public SourceDataProvider SourceDataProvider { get; set; } = sourceDataProvider;
    }
}
