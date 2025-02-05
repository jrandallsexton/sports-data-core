using SportsData.Core.Common;
using SportsData.Provider.Infrastructure.Data.Entities;

namespace SportsData.Provider.Infrastructure.Data.Seeders
{
    public class GolfSeeder : SeederBase, ISeedResourceIndexes
    {
        public List<ResourceIndex> Generate(Sport sport, List<int> seasons)
        {
            var league = "pga";

            var values = new List<ResourceIndex>();

            base.GenerateNonSeasonalResources(values, sport, "golf", league);

            foreach (var season in seasons)
            {
                base.GenerateSeasonalResources(values, sport, "golf", league, season);
            }

            values.Add(new ResourceIndex()
            {
                Id = Guid.NewGuid(),
                Provider = SourceDataProvider.Espn,
                SportId = sport,
                DocumentType = DocumentType.GolfCalendar,
                Endpoint = $"http://sports.core.api.espn.com/v2/sports/golf/leagues/{league}/calendar",
                EndpointMask = null,
                CreatedBy = Guid.Empty,
                IsSeasonSpecific = true,
                IsEnabled = true,
                SeasonYear = 2025,
                Ordinal = values.Count
            });

            return values;
        }
    }
}
