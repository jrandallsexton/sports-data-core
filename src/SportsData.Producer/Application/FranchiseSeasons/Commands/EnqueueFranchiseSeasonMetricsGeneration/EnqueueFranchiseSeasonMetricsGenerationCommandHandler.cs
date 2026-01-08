using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Processing;
using SportsData.Producer.Application.FranchiseSeasons.Commands.CalculateFranchiseSeasonMetrics;
using SportsData.Producer.Application.GroupSeasons;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.FranchiseSeasons.Commands.EnqueueFranchiseSeasonMetricsGeneration;

public interface IEnqueueFranchiseSeasonMetricsGenerationCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(
        EnqueueFranchiseSeasonMetricsGenerationCommand command,
        CancellationToken cancellationToken = default);
}

public class EnqueueFranchiseSeasonMetricsGenerationCommandHandler : IEnqueueFranchiseSeasonMetricsGenerationCommandHandler
{
    private readonly ILogger<EnqueueFranchiseSeasonMetricsGenerationCommandHandler> _logger;
    private readonly TeamSportDataContext _dataContext;
    private readonly IProvideBackgroundJobs _backgroundJobProvider;
    private readonly IGroupSeasonsService _groupSeasonsService;

    public EnqueueFranchiseSeasonMetricsGenerationCommandHandler(
        ILogger<EnqueueFranchiseSeasonMetricsGenerationCommandHandler> logger,
        TeamSportDataContext dataContext,
        IProvideBackgroundJobs backgroundJobProvider,
        IGroupSeasonsService groupSeasonsService)
    {
        _logger = logger;
        _dataContext = dataContext;
        _backgroundJobProvider = backgroundJobProvider;
        _groupSeasonsService = groupSeasonsService;
    }

    public async Task<Result<Guid>> ExecuteAsync(
        EnqueueFranchiseSeasonMetricsGenerationCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "EnqueueFranchiseSeasonMetricsGeneration started. SeasonYear={SeasonYear}, Sport={Sport}",
            command.SeasonYear,
            command.Sport);

        var fbsGroupIds = await _groupSeasonsService.GetFbsGroupSeasonIds(command.SeasonYear);

        var franchiseSeasons = await _dataContext.FranchiseSeasons
            .Include(fs => fs.Franchise)
            .Where(fs =>
                fs.GroupSeasonId != null &&
                fs.SeasonYear == command.SeasonYear &&
                fs.Franchise.Sport == command.Sport &&
                fbsGroupIds.Contains(fs.GroupSeasonId!.Value))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Found {Count} franchise seasons to process. SeasonYear={SeasonYear}, Sport={Sport}",
            franchiseSeasons.Count,
            command.SeasonYear,
            command.Sport);

        var correlationId = Guid.NewGuid();

        foreach (var fs in franchiseSeasons)
        {
            var calculateCommand = new CalculateFranchiseSeasonMetricsCommand(fs.Id, command.SeasonYear);
            _backgroundJobProvider.Enqueue<ICalculateFranchiseSeasonMetricsCommandHandler>(
                x => x.ExecuteAsync(calculateCommand, CancellationToken.None));
        }

        _logger.LogInformation(
            "EnqueueFranchiseSeasonMetricsGeneration completed. SeasonYear={SeasonYear}, EnqueuedCount={Count}, CorrelationId={CorrelationId}",
            command.SeasonYear,
            franchiseSeasons.Count,
            correlationId);

        return new Success<Guid>(correlationId, ResultStatus.Accepted);
    }
}
