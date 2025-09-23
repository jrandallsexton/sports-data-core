using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Canonical.Models;

namespace SportsData.Api.Application.UI.Contest
{
    public interface IContestService
    {
        Task<ContestOverviewDto> GetContestOverviewByContestId(Guid contestId);
    }

    public class ContestService : IContestService
    {
        private readonly ILogger<ContestService> _logger;
        private readonly IProvideCanonicalData _canonicalDataProvider;

        public ContestService(
            ILogger<ContestService> logger,
            IProvideCanonicalData canonicalDataProvider)
        {
            _logger = logger;
            _canonicalDataProvider = canonicalDataProvider;
        }

        public async Task<ContestOverviewDto> GetContestOverviewByContestId(Guid contestId) =>
            await _canonicalDataProvider.GetContestOverviewByContestId(contestId);
    }
}
