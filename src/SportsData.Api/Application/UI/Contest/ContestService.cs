using SportsData.Api.Infrastructure.Data;
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
        Task<Result<bool>> SubmitContestPredictions(Guid userId, List<ContestPredictionDto> predictions);
    }

    public class ContestService : IContestService
    {
        private readonly ILogger<ContestService> _logger;
        private readonly IProvideCanonicalData _canonicalDataProvider;
        private readonly AppDataContext _dataContext;

        public ContestService(
            ILogger<ContestService> logger,
            IProvideCanonicalData canonicalDataProvider,
            AppDataContext dataContext
            )
        {
            _logger = logger;
            _canonicalDataProvider = canonicalDataProvider;
            _dataContext = dataContext;
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

        public async Task<Result<bool>> SubmitContestPredictions(Guid userId, List<ContestPredictionDto> predictions)
        {
            foreach (var entity in predictions.Select(prediction => prediction.AsEntity()))
            {
                entity.CreatedBy = userId;
                entity.CreatedUtc = DateTime.UtcNow;

                await _dataContext.ContestPredictions.AddAsync(entity);
            }

            await _dataContext.SaveChangesAsync();

            return new Success<bool>(true);
        }
    }
}
