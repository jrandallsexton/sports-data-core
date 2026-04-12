using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Football.Entities
{
    public class FootballContest : ContestBase
    {
        public ICollection<FootballCompetition> Competitions { get; set; } = [];
    }
}
