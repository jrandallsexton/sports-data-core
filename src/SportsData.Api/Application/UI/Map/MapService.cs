using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Canonical.Models;

namespace SportsData.Api.Application.UI.Map
{
    public interface IMapService
    {
        Task<List<Matchup>> GetMatchupsForCurrentWeek();
    }

    public class MapService : IMapService
    {
        private readonly IProvideCanonicalData _canonicalDataProvider;

        public MapService(IProvideCanonicalData canonicalDataProvider)
        {
            _canonicalDataProvider = canonicalDataProvider;
        }

        public async Task<List<Matchup>> GetMatchupsForCurrentWeek()
        {
            var allMatchups = await _canonicalDataProvider.GetMatchupsForCurrentWeek();
            return allMatchups;
        }
    }
}
