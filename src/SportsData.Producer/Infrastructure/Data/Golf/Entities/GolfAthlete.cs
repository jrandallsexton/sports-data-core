using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Infrastructure.Data.Golf.Entities
{
    public class GolfAthlete : Athlete
    {
        public required string Hand { get; set; }
    }
}
