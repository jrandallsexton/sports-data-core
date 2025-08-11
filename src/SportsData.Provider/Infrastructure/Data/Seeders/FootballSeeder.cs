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

            // default resource index for external document sourcing requests
            values.Add(new ResourceIndex
            {
                Id = Guid.Empty,
                CreatedBy = Guid.Empty,
                CreatedUtc = DateTime.UtcNow,
                DocumentType = DocumentType.Unknown,
                EndpointMask = null,
                IsEnabled = false,
                IsQueued = false,
                IsRecurring = false,
                IsSeasonSpecific = false,
                Name = $"{sport} - {league} - Default",
                Ordinal = 0,
                Provider = SourceDataProvider.Espn,
                SourceUrlHash = "default-url-hash",
                SportId = sport,
                Uri = new Uri("http://domain.none")
            });

            base.GenerateNonSeasonalResources(values, sport, "football", league);

            //foreach (var season in seasons)
            //{
            //    base.GenerateSeasonalResources(values, sport, "football", league, season);
            //}

            return values;
        }
    }
}
