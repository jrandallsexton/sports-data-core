using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Processing;
using SportsData.Producer.Application.FranchiseSeasons.Commands.CalculateFranchiseSeasonMetrics;
using SportsData.Producer.Application.GroupSeasons;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

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

        // FBS scoping is an NCAA concept — the NFL hierarchy has no FBS
        // root, so asking for one 500'd and NFL season metrics never
        // generated at all. NFL (and any future non-NCAA football) takes
        // every franchise season for the year.
        HashSet<Guid>? fbsGroupIds = command.Sport == Sport.FootballNcaa
            ? await _groupSeasonsService.GetFbsGroupSeasonIds(command.SeasonYear)
            : null;

        // Only the ids are needed; the Sport predicate translates through
        // the Franchise navigation without an Include.
        var franchiseSeasonIds = await _dataContext.FranchiseSeasons
            .Where(fs =>
                fs.SeasonYear == command.SeasonYear &&
                fs.Franchise.Sport == command.Sport &&
                (fbsGroupIds == null ||
                 (fs.GroupSeasonId != null && fbsGroupIds.Contains(fs.GroupSeasonId!.Value))))
            .Select(fs => fs.Id)
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Found {Count} franchise seasons to process. SeasonYear={SeasonYear}, Sport={Sport}",
            franchiseSeasonIds.Count,
            command.SeasonYear,
            command.Sport);

        var correlationId = Guid.NewGuid();

        foreach (var franchiseSeasonId in franchiseSeasonIds)
        {
            var calculateCommand = new CalculateFranchiseSeasonMetricsCommand(franchiseSeasonId, command.SeasonYear);
            _backgroundJobProvider.Enqueue<ICalculateFranchiseSeasonMetricsCommandHandler>(
                x => x.ExecuteAsync(calculateCommand, CancellationToken.None));
        }

        _logger.LogInformation(
            "EnqueueFranchiseSeasonMetricsGeneration completed. SeasonYear={SeasonYear}, EnqueuedCount={Count}, CorrelationId={CorrelationId}",
            command.SeasonYear,
            franchiseSeasonIds.Count,
            correlationId);

        return new Success<Guid>(correlationId, ResultStatus.Accepted);
    }
}
