using SportsData.Core.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class ExternalId
    {
        public string Id { get; set; }

        public SourceDataProvider Provider { get; set; }
    }
}
