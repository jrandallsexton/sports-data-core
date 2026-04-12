using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Golf.Entities
{
    public class GolfAthlete : AthleteBase
    {
        public required string Hand { get; set; }
    }
}
