using SportsData.Core.Common;
using SportsData.Provider.Infrastructure.Data.Entities;

namespace SportsData.Provider.Infrastructure.Data.Seeders
{
    public class BasketballSeeder : SeederBase, ISeedResourceIndexes
    {
        public List<RecurringJob> Generate(Sport sport, List<int> seasons)
        {
            var league = "nba";

            var values = new List<RecurringJob>();

            base.GenerateNonSeasonalResources(values, sport, "basketball", league);

            foreach (var season in seasons)
            {
                base.GenerateSeasonalResources(values, sport, "basketball", league, season);
            }

            return values;
        }
    }
}
