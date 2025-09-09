using SportsData.Api.Application.UI.Rankings.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Canonical;

namespace SportsData.Api.Application.UI.Rankings
{
    public interface IRankingsService
    {
        Task<RankingsByPollIdByWeekDto> GetRankingsByPollWeek(int seasonYear, int week, CancellationToken ct);
    }

    public class RankingsService : IRankingsService
    {
        private readonly ILogger<RankingsService> _logger;
        private readonly IProvideCanonicalData _canonicalDataProvider;
        private readonly AppDataContext _dataContext;

        public RankingsService(
            ILogger<RankingsService> logger,
            IProvideCanonicalData canonicalDataProvider,
            AppDataContext dataContext)
        {
            _logger = logger;
            _canonicalDataProvider = canonicalDataProvider;
            _dataContext = dataContext;
        }

        public async Task<RankingsByPollIdByWeekDto> GetRankingsByPollWeek(
            int seasonYear,
            int seasonWeek,
            CancellationToken ct)
        {
            var rankings = await _canonicalDataProvider.GetRankingsByPollIdByWeek(
                "ap", seasonYear, seasonWeek);

            return rankings;
        }
    }
}
