using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Contest.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.UI.Contest.Commands.SubmitContestPredictions;

public interface ISubmitContestPredictionsCommandHandler
{
    Task<Result<bool>> ExecuteAsync(
        SubmitContestPredictionsCommand command,
        CancellationToken cancellationToken = default);
}

public class SubmitContestPredictionsCommandHandler : ISubmitContestPredictionsCommandHandler
{
    private readonly ILogger<SubmitContestPredictionsCommandHandler> _logger;
    private readonly AppDataContext _dataContext;

    public SubmitContestPredictionsCommandHandler(
        ILogger<SubmitContestPredictionsCommandHandler> logger,
        AppDataContext dataContext)
    {
        _logger = logger;
        _dataContext = dataContext;
    }

    public async Task<Result<bool>> ExecuteAsync(
        SubmitContestPredictionsCommand command,
        CancellationToken cancellationToken = default)
    {
        var correlationId = ActivityExtensions.GetCorrelationId();

        if (command.Predictions == null || !command.Predictions.Any())
        {
            return new Failure<bool>(
                false,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(command.Predictions), "No predictions provided")]);
        }

        _logger.LogInformation(
            "SubmitPredictions initiated. UserId={UserId}, PredictionCount={Count}, CorrelationId={CorrelationId}",
            command.UserId,
            command.Predictions.Count,
            correlationId);

        var contestIds = command.Predictions.Select(p => p.ContestId).Distinct().ToList();

        // Delete existing predictions for these contests and this user
        var existingPredictions = await _dataContext.ContestPredictions
            .Where(cp => contestIds.Contains(cp.ContestId) && cp.CreatedBy == command.UserId)
            .ToListAsync(cancellationToken);

        if (existingPredictions.Any())
        {
            _dataContext.ContestPredictions.RemoveRange(existingPredictions);
        }

        // Add new predictions
        foreach (var entity in command.Predictions.Select(prediction => prediction.AsEntity()))
        {
            entity.CreatedBy = command.UserId;
            entity.CreatedUtc = DateTime.UtcNow;

            await _dataContext.ContestPredictions.AddAsync(entity, cancellationToken);
        }

        await _dataContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Successfully submitted predictions. UserId={UserId}, Count={Count}, CorrelationId={CorrelationId}",
            command.UserId,
            command.Predictions.Count,
            correlationId);

        return new Success<bool>(true);
    }
}
