using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities.Metrics
{
    public class ModelRun : CanonicalEntityBase<Guid>
    {
        public string ModelName { get; set; } = null!;

        public string Version { get; set; } = "v1";

        public string? ParametersJson { get; set; }

        public DateTime ComputedUtc { get; set; }
    }
}
