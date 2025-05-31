using SportsData.Core.Common;
using SportsData.Provider.Infrastructure.Data.Entities;

namespace SportsData.Provider.Application.Jobs.Definitions
{
    public class DocumentJobDefinition
    {
        public DocumentJobDefinition()
        {
            
        }

        public DocumentJobDefinition(RecurringJob resourceIndex)
        {
            Sport = resourceIndex.SportId;
            SourceDataProvider = resourceIndex.Provider;
            DocumentType = resourceIndex.DocumentType;
            Endpoint = resourceIndex.Endpoint;
            EndpointMask = resourceIndex.EndpointMask;
            SeasonYear = resourceIndex.SeasonYear;
            ResourceIndexId = resourceIndex.Id;
        }

        public DocumentJobDefinition(ScheduledJob task)
        {
            Sport = task.Sport;
            SourceDataProvider = task.SourceDataProvider;
            DocumentType = task.DocumentType;
            Endpoint = task.Href;
            EndpointMask = string.Empty; // TODO: Do I need this?
            SeasonYear = task.SeasonYear;
            ResourceIndexId = task.Id;
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
