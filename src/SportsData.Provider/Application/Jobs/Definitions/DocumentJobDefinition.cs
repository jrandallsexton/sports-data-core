using SportsData.Core.Common;
using SportsData.Provider.Infrastructure.Data.Entities;

namespace SportsData.Provider.Application.Jobs.Definitions
{
    public class DocumentJobDefinition(ResourceIndex resourceIndex)
    {
        public Sport Sport { get; init; } = resourceIndex.SportId;

        public SourceDataProvider SourceDataProvider { get; init; } = resourceIndex.ProviderId;

        public DocumentType DocumentType { get; init; } = resourceIndex.DocumentTypeId;

        public string Endpoint { get; init; } = resourceIndex.Endpoint;

        public string EndpointMask { get; init; } = resourceIndex.EndpointMask;

        public int? SeasonYear { get; init; } = resourceIndex.SeasonYear;
    }
}
