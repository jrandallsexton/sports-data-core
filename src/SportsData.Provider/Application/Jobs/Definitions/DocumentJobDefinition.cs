using SportsData.Core.Common;
using SportsData.Provider.Infrastructure.Data.Entities;

namespace SportsData.Provider.Application.Jobs.Definitions
{
    public class DocumentJobDefinition
    {
        public DocumentJobDefinition()
        {
            
        }

        public DocumentJobDefinition(Infrastructure.Data.Entities.ResourceIndex resourceIndex)
        {
            DocumentType = resourceIndex.DocumentType;
            Shape = resourceIndex.Shape;
            Endpoint = resourceIndex.Uri;
            EndpointMask = resourceIndex.EndpointMask;
            ResourceIndexId = resourceIndex.Id;
            SeasonYear = resourceIndex.SeasonYear;
            SourceDataProvider = resourceIndex.Provider;
            Sport = resourceIndex.SportId;
            StartPage = resourceIndex.LastPageIndex ?? 1; // Default to page 1 if not set
        }

        public DocumentJobDefinition(ScheduledJob task)
        {
            DocumentType = task.DocumentType;
            Endpoint = task.Uri;
            EndpointMask = string.Empty; // TODO: Do I need this?
            ResourceIndexId = task.Id;
            SeasonYear = task.SeasonYear;
            SourceDataProvider = task.SourceDataProvider;
            Sport = task.Sport;
        }

        public Guid ResourceIndexId { get; set; }

        public ResourceShape Shape { get; set; } = ResourceShape.Auto;

        public Sport Sport { get; init; }

        public SourceDataProvider SourceDataProvider { get; init; }

        public DocumentType DocumentType { get; init; }

        public Uri? Endpoint { get; init; }

        public string? EndpointMask { get; init; }

        public int? SeasonYear { get; init; }

        public int? StartPage { get; set; }

        /// <summary>
        /// Optional inclusion-only list of linked document types that downstream processors
        /// should honor when deciding which linked documents to spawn.
        /// If null or empty, all linked documents are processed (default behavior).
        /// If provided and non-empty, only linked documents of types in this collection will be spawned.
        /// </summary>
        public IReadOnlyCollection<DocumentType>? IncludeLinkedDocumentTypes { get; set; }
    }
}
