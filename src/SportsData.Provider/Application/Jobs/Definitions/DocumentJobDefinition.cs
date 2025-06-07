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
            Endpoint = resourceIndex.Url;
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
            Endpoint = task.Href;
            EndpointMask = string.Empty; // TODO: Do I need this?
            ResourceIndexId = task.Id;
            SeasonYear = task.SeasonYear;
            SourceDataProvider = task.SourceDataProvider;
            Sport = task.Sport;
        }

        public Guid ResourceIndexId { get; set; }

        public Sport Sport { get; init; }

        public SourceDataProvider SourceDataProvider { get; init; }

        public DocumentType DocumentType { get; init; }

        public string Endpoint { get; init; }

        public string? EndpointMask { get; init; }

        public int? SeasonYear { get; init; }

        public int? StartPage { get; set; }
    }
}
