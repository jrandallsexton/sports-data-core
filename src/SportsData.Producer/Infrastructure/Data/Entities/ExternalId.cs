using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class ExternalId : CanonicalEntityBase<Guid>, IHasSourceUrlHash
    {
        public required string Value { get; set; }

        public SourceDataProvider Provider { get; set; }

        public required string SourceUrlHash { get; set; }
    }
}
