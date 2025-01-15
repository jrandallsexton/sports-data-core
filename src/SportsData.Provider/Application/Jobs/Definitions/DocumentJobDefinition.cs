using SportsData.Core.Common;

namespace SportsData.Provider.Application.Jobs.Definitions
{
    public abstract class DocumentJobDefinition
    {
        public abstract SourceDataProvider SourceDataProvider { get; init; }

        public abstract DocumentType DocumentType { get; init; }

        public abstract string Endpoint { get; init; }

        public abstract string EndpointMask { get; init; }

        public abstract int? SeasonYear { get; init; }
    }
}
