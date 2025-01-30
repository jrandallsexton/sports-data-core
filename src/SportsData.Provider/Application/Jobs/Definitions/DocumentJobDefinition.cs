using SportsData.Core.Common;
using SportsData.Provider.Infrastructure.Data.Entities;

namespace SportsData.Provider.Application.Jobs.Definitions
{
    public class DocumentJobDefinition
    {
        public DocumentJobDefinition()
        {
            
        }

        public DocumentJobDefinition(ResourceIndex resourceIndex)
        {
            Sport = resourceIndex.SportId;
            SourceDataProvider = resourceIndex.Provider;
            DocumentType = resourceIndex.DocumentType;
            Endpoint = resourceIndex.Endpoint;
            EndpointMask = resourceIndex.EndpointMask;
            SeasonYear = resourceIndex.SeasonYear;
            ResourceIndexId = resourceIndex.Id;
        }

        public Guid ResourceIndexId { get; set; }

        public Sport Sport { get; init; }

        public SourceDataProvider SourceDataProvider { get; init; }

        public DocumentType DocumentType { get; init; }

        public string Endpoint { get; init; }

        public string? EndpointMask { get; init; }

        public int? SeasonYear { get; init; }
    }
}
