using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Common
{
    public abstract class ExternalId : CanonicalEntityBase<Guid>, IHasSourceUrlHash
    {
        public required string Value { get; set; }

        public SourceDataProvider Provider { get; set; }

        public required string SourceUrl { get; set; }

        public required string SourceUrlHash { get; set; }
    }
}
