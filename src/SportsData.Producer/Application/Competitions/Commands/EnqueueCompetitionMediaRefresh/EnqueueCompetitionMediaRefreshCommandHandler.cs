using SportsData.Core.Common;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Competitions.Commands.RefreshCompetitionMedia;

namespace SportsData.Producer.Application.Competitions.Commands.EnqueueCompetitionMediaRefresh;

public interface IEnqueueCompetitionMediaRefreshCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(
        EnqueueCompetitionMediaRefreshCommand command,
        CancellationToken cancellationToken = default);
}

public class EnqueueCompetitionMediaRefreshCommandHandler : IEnqueueCompetitionMediaRefreshCommandHandler
{
    private readonly IProvideBackgroundJobs _backgroundJobProvider;

    public EnqueueCompetitionMediaRefreshCommandHandler(IProvideBackgroundJobs backgroundJobProvider)
    {
        _backgroundJobProvider = backgroundJobProvider;
    }

    public Task<Result<Guid>> ExecuteAsync(
        EnqueueCompetitionMediaRefreshCommand command,
        CancellationToken cancellationToken = default)
    {
        var refreshCommand = new RefreshCompetitionMediaCommand(command.CompetitionId, command.RemoveExisting);
        _backgroundJobProvider.Enqueue<IRefreshCompetitionMediaCommandHandler>(
            h => h.ExecuteAsync(refreshCommand, CancellationToken.None));

        return Task.FromResult<Result<Guid>>(new Success<Guid>(command.CompetitionId, ResultStatus.Accepted));
    }
}
