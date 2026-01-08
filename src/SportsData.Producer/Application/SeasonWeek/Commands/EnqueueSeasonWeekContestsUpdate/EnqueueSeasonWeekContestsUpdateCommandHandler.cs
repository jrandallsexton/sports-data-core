using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Contests;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.SeasonWeek.Commands.EnqueueSeasonWeekContestsUpdate;

public interface IEnqueueSeasonWeekContestsUpdateCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(
        EnqueueSeasonWeekContestsUpdateCommand command,
        CancellationToken cancellationToken = default);
}

public class EnqueueSeasonWeekContestsUpdateCommandHandler : IEnqueueSeasonWeekContestsUpdateCommandHandler
{
    private readonly ILogger<EnqueueSeasonWeekContestsUpdateCommandHandler> _logger;
    private readonly TeamSportDataContext _dataContext;
    private readonly IProvideBackgroundJobs _backgroundJobProvider;

    public EnqueueSeasonWeekContestsUpdateCommandHandler(
        ILogger<EnqueueSeasonWeekContestsUpdateCommandHandler> logger,
        TeamSportDataContext dataContext,
        IProvideBackgroundJobs backgroundJobProvider)
    {
        _logger = logger;
        _dataContext = dataContext;
        _backgroundJobProvider = backgroundJobProvider;
    }

    public async Task<Result<Guid>> ExecuteAsync(
        EnqueueSeasonWeekContestsUpdateCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "EnqueueSeasonWeekContestsUpdate started. SeasonWeekId={SeasonWeekId}",
            command.SeasonWeekId);

        var contestIds = await _dataContext.Contests
            .Where(c => c.SeasonWeekId == command.SeasonWeekId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Found {Count} contests to update. SeasonWeekId={SeasonWeekId}",
            contestIds.Count,
            command.SeasonWeekId);

        foreach (var contestId in contestIds)
        {
            var cmd = new UpdateContestCommand(
                contestId,
                SourceDataProvider.Espn,
                Sport.FootballNcaa, // TODO: remove hard-coding
                Guid.NewGuid());
            _backgroundJobProvider.Enqueue<IUpdateContests>(p => p.Process(cmd));
        }

        _logger.LogInformation(
            "EnqueueSeasonWeekContestsUpdate completed. SeasonWeekId={SeasonWeekId}, EnqueuedCount={Count}",
            command.SeasonWeekId,
            contestIds.Count);

        return new Success<Guid>(command.SeasonWeekId, ResultStatus.Accepted);
    }
}
