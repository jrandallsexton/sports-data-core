using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Common
{
    public abstract class TeamAthlete : Athlete
    {
        public Guid? FranchiseId { get; set; }

        public Guid? FranchiseSeasonId { get; set; }

        public Guid CurrentPosition { get; set; }

        public AthletePosition Position { get; set; } = null!;
    }
}
