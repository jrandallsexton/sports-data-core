using Microsoft.EntityFrameworkCore;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.UI.Contest
{
    public interface IContestService
    {
        Task<Result<ContestOverviewDto>> GetContestOverviewByContestId(Guid contestId);
        Task<Result<Guid>> RefreshContestByContestId(Guid contestId);
        Task<Result<Guid>> RefreshContestMediaByContestId(Guid contestId);
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
            var correlationId = ActivityExtensions.GetCorrelationId();
            
            try
            {
                _logger.LogInformation(
                    "GetContestOverview requested. ContestId={ContestId}, CorrelationId={CorrelationId}",
                    contestId,
                    correlationId);
                    
                var result = await _canonicalDataProvider.GetContestOverviewByContestId(contestId);
                
                if (result == null)
                {
                    _logger.LogWarning(
                        "Contest overview not found. ContestId={ContestId}, CorrelationId={CorrelationId}",
                        contestId,
                        correlationId);
                    return new Failure<ContestOverviewDto>(
                        default!,
                        ResultStatus.NotFound,
                        [new FluentValidation.Results.ValidationFailure(nameof(contestId), $"Contest with ID {contestId} not found")]
                    );
                }
                
                return new Success<ContestOverviewDto>(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error getting contest overview. ContestId={ContestId}, CorrelationId={CorrelationId}",
                    contestId,
                    correlationId);
                return new Failure<ContestOverviewDto>(
                    default!,
                    ResultStatus.BadRequest,
                    [new FluentValidation.Results.ValidationFailure("Error", ex.Message)]
                );
            }
        }

        public async Task<Result<Guid>> RefreshContestByContestId(Guid contestId)
        {
            var correlationId = ActivityExtensions.GetCorrelationId();
            
            try
            {
                _logger.LogInformation(
                    "RefreshContest initiated. ContestId={ContestId}, CorrelationId={CorrelationId}",
                    contestId,
                    correlationId);
                    
                await _canonicalDataProvider.RefreshContestByContestId(contestId);
                
                _logger.LogInformation(
                    "RefreshContest completed. ContestId={ContestId}, CorrelationId={CorrelationId}",
                    contestId,
                    correlationId);
                    
                return new Success<Guid>(correlationId, ResultStatus.Accepted);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error refreshing contest. ContestId={ContestId}, CorrelationId={CorrelationId}",
                    contestId,
                    correlationId);
                return new Failure<Guid>(
                    default,
                    ResultStatus.BadRequest,
                    [new FluentValidation.Results.ValidationFailure("Error", ex.Message)]
                );
            }
        }

        public async Task<Result<Guid>> RefreshContestMediaByContestId(Guid contestId)
        {
            var correlationId = ActivityExtensions.GetCorrelationId();
            
            try
            {
                _logger.LogInformation(
                    "RefreshContestMedia initiated. ContestId={ContestId}, CorrelationId={CorrelationId}",
                    contestId,
                    correlationId);
                    
                await _canonicalDataProvider.RefreshContestMediaByContestId(contestId);
                
                _logger.LogInformation(
                    "RefreshContestMedia completed. ContestId={ContestId}, CorrelationId={CorrelationId}",
                    contestId,
                    correlationId);
                    
                return new Success<Guid>(correlationId, ResultStatus.Accepted);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error refreshing contest media. ContestId={ContestId}, CorrelationId={CorrelationId}",
                    contestId,
                    correlationId);
                return new Failure<Guid>(
                    default,
                    ResultStatus.BadRequest,
                    [new FluentValidation.Results.ValidationFailure("Error", ex.Message)]
                );
            }
        }

        public async Task<Result<bool>> SubmitContestPredictions(
            Guid userId,
            List<ContestPredictionDto> predictions)
        {
            var correlationId = ActivityExtensions.GetCorrelationId();
            
            try
            {
                if (predictions == null || !predictions.Any())
                {
                    return new Failure<bool>(
                        false,
                        ResultStatus.Validation,
                        [new FluentValidation.Results.ValidationFailure(nameof(predictions), "No predictions provided")]
                    );
                }

                _logger.LogInformation(
                    "SubmitPredictions initiated. UserId={UserId}, PredictionCount={Count}, CorrelationId={CorrelationId}",
                    userId,
                    predictions.Count,
                    correlationId);

                var contestIds = predictions.Select(p => p.ContestId).Distinct().ToList();

                // Delete existing predictions for these contests and this user
                var existingPredictions = await _dataContext.ContestPredictions
                    .Where(cp => contestIds.Contains(cp.ContestId) && cp.CreatedBy == userId)
                    .ToListAsync();

                if (existingPredictions.Any())
                {
                    _dataContext.ContestPredictions.RemoveRange(existingPredictions);
                }

                // Add new predictions
                foreach (var entity in predictions.Select(prediction => prediction.AsEntity()))
                {
                    entity.CreatedBy = userId;
                    entity.CreatedUtc = DateTime.UtcNow;

                    await _dataContext.ContestPredictions.AddAsync(entity);
                }

                await _dataContext.SaveChangesAsync();
                
                _logger.LogInformation(
                    "Successfully submitted predictions. UserId={UserId}, Count={Count}, CorrelationId={CorrelationId}", 
                    userId,
                    predictions.Count,
                    correlationId);

                return new Success<bool>(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex, 
                    "Error submitting predictions. UserId={UserId}, CorrelationId={CorrelationId}", 
                    userId,
                    correlationId);
                return new Failure<bool>(
                    false,
                    ResultStatus.BadRequest,
                    [new FluentValidation.Results.ValidationFailure("Error", ex.Message)]
                );
            }
        }
    }
}
