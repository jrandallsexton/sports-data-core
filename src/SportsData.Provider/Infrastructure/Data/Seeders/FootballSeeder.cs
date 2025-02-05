using SportsData.Core.Common;
using SportsData.Provider.Infrastructure.Data.Entities;

namespace SportsData.Provider.Infrastructure.Data.Seeders
{
    public class FootballSeeder : SeederBase, ISeedResourceIndexes
    {
        public List<ResourceIndex> Generate(Sport sport, List<int> seasons)
        {
            var league = sport == Sport.FootballNcaa ? "college-football" : "nfl";
            
            var values = new List<ResourceIndex>();

            base.GenerateNonSeasonalResources(values, sport, "football", league);

            foreach (var season in seasons)
            {
                base.GenerateSeasonalResources(values, sport, "football", league, season);
            }

            return values;
        }
    }
}
