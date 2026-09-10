using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Processing;
using SportsData.Producer.Application.FranchiseSeasons.Commands.CalculateFranchiseSeasonMetrics;
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

    public EnqueueFranchiseSeasonMetricsGenerationCommandHandler(
        ILogger<EnqueueFranchiseSeasonMetricsGenerationCommandHandler> logger,
        TeamSportDataContext dataContext,
        IProvideBackgroundJobs backgroundJobProvider)
    {
        _logger = logger;
        _dataContext = dataContext;
        _backgroundJobProvider = backgroundJobProvider;
    }

    public async Task<Result<Guid>> ExecuteAsync(
        EnqueueFranchiseSeasonMetricsGenerationCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "EnqueueFranchiseSeasonMetricsGeneration started. SeasonYear={SeasonYear}, Sport={Sport}",
            command.SeasonYear,
            command.Sport);

        // EVERY franchise season for the year — no FBS scoping (dropped
        // 2026-09-10). The old FBS filter starved every FCS team of season
        // metrics even though their FBS matchups produce CompetitionMetric
        // rows (370 teams had per-game metrics; only 129 got season rows),
        // which in turn nulled BOTH sides' metrics in FBS-vs-FCS previews
        // via the both-or-nothing rule. Calculate is a graceful no-op for
        // teams with no per-game metrics, so the wider fan-out costs only
        // cheap no-op jobs. Dropping the FBS lookup also removes the
        // "FBS group root(s) not found" throw for unsourced years entirely.
        var franchiseSeasonIds = await _dataContext.FranchiseSeasons
            .AsNoTracking()
            .Where(fs =>
                fs.SeasonYear == command.SeasonYear &&
                fs.Franchise.Sport == command.Sport)
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
