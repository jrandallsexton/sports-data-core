using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class Team : EntityBase<Guid>
    {
        public Guid FranchiseId { get; set; }

        public int Season { get; set; }

        public int Wins { get; set; }

        public int Losses { get; set; }

        public int Ties { get; set; }
    }
}
