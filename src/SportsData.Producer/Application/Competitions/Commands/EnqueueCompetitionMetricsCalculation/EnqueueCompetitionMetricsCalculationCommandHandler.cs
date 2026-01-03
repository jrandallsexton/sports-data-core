using SportsData.Core.Common;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Competitions.Commands.CalculateCompetitionMetrics;

namespace SportsData.Producer.Application.Competitions.Commands.EnqueueCompetitionMetricsCalculation;

public interface IEnqueueCompetitionMetricsCalculationCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(
        EnqueueCompetitionMetricsCalculationCommand command,
        CancellationToken cancellationToken = default);
}

public class EnqueueCompetitionMetricsCalculationCommandHandler : IEnqueueCompetitionMetricsCalculationCommandHandler
{
    private readonly IProvideBackgroundJobs _backgroundJobProvider;

    public EnqueueCompetitionMetricsCalculationCommandHandler(IProvideBackgroundJobs backgroundJobProvider)
    {
        _backgroundJobProvider = backgroundJobProvider;
    }

    public Task<Result<Guid>> ExecuteAsync(
        EnqueueCompetitionMetricsCalculationCommand command,
        CancellationToken cancellationToken = default)
    {
        var calculateCommand = new CalculateCompetitionMetricsCommand(command.CompetitionId);
        _backgroundJobProvider.Enqueue<ICalculateCompetitionMetricsCommandHandler>(
            h => h.ExecuteAsync(calculateCommand, CancellationToken.None));

        return Task.FromResult<Result<Guid>>(new Success<Guid>(command.CompetitionId, ResultStatus.Accepted));
    }
}
