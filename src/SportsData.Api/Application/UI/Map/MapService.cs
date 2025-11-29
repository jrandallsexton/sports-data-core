using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Canonical.Models;

namespace SportsData.Api.Application.UI.Map
{
    public interface IMapService
    {
        Task<GetMapMatchupsResponse> GetMatchups(GetMapMatchupsQuery query);
    }

    public class MapService : IMapService
    {
        private readonly IProvideCanonicalData _canonicalDataProvider;

        public MapService(IProvideCanonicalData canonicalDataProvider)
        {
            _canonicalDataProvider = canonicalDataProvider;
        }

        public async Task<GetMapMatchupsResponse> GetMatchups(GetMapMatchupsQuery query)
        { 
            if (query.LeagueId is null && query.WeekNumber is null)
            {
                var matchups = await _canonicalDataProvider
                    .GetMatchupsForCurrentWeek();

                return new GetMapMatchupsResponse
                {
                    Matchups = matchups
                };

            }
            else if (query.LeagueId is null)
            {
                // we have a league, but no week
                var matchups = await _canonicalDataProvider
                    .GetMatchupsForSeasonWeek(2025, query.WeekNumber!.Value);

                return new GetMapMatchupsResponse
                {
                    Matchups = matchups
                };

            }
            else
            {
                // we have a week, but no league
                var matchups = await _canonicalDataProvider
                    .GetMatchupsForCurrentWeek();

                return new GetMapMatchupsResponse
                {
                    Matchups = matchups
                };
            }
        }

        public async Task<List<Matchup>> GetMatchupsForCurrentWeek()
        {
            var allMatchups = await _canonicalDataProvider.GetMatchupsForCurrentWeek();
            return allMatchups;
        }
    }

    public class GetMapMatchupsQuery
    {
        public Guid? LeagueId { get; set; }

        public int? WeekNumber { get; set; }
    }

    public class GetMapMatchupsResponse
    {
        public List<Matchup> Matchups { get; set; } = [];
    }
}
