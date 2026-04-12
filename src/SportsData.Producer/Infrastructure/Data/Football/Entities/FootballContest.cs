using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Football.Entities
{
    public class FootballContest : Contest
    {
        public ICollection<FootballCompetition> Competitions { get; set; } = [];
    }
}
