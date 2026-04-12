using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Baseball.Entities
{
    public class BaseballContest : ContestBase
    {
        public ICollection<BaseballCompetition> Competitions { get; set; } = [];
    }
}
