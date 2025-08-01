using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class AthleteSeasonSplit : CanonicalEntityBase<Guid>
    {
        public required string Name { get; set; }

        public required string Abbreviation { get; set; }

        public required string Type { get; set; }

        public ICollection<AthleteSeasonStatistic> Statistics = [];
    }
}
