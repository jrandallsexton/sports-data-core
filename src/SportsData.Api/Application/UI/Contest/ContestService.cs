using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;

namespace SportsData.Api.Application.UI.Contest
{
    public interface IContestService
    {
        Task<Result<ContestOverviewDto>> GetContestOverviewByContestId(Guid contestId);
        Task RefreshContestByContestId(Guid contestId);
        Task RefreshContestMediaByContestId(Guid contestId);
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

        public async Task<Result<ContestOverviewDto>> GetContestOverviewByContestId(Guid contestId)
        {
            var result = await _canonicalDataProvider.GetContestOverviewByContestId(contestId);
            return new Success<ContestOverviewDto>(result);
        }

        public async Task RefreshContestByContestId(Guid contestId)
        {
            await _canonicalDataProvider.RefreshContestByContestId(contestId);
        }

        public async Task RefreshContestMediaByContestId(Guid contestId)
        {
            await _canonicalDataProvider.RefreshContestMediaByContestId(contestId);
        }
    }
}
