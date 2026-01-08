using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Metrics;

namespace SportsData.Producer.Application.FranchiseSeasons.Commands.CalculateFranchiseSeasonMetrics;

public interface ICalculateFranchiseSeasonMetricsCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(
        CalculateFranchiseSeasonMetricsCommand command,
        CancellationToken cancellationToken = default);
}

public class CalculateFranchiseSeasonMetricsCommandHandler : ICalculateFranchiseSeasonMetricsCommandHandler
{
    private readonly ILogger<CalculateFranchiseSeasonMetricsCommandHandler> _logger;
    private readonly TeamSportDataContext _dataContext;

    public CalculateFranchiseSeasonMetricsCommandHandler(
        ILogger<CalculateFranchiseSeasonMetricsCommandHandler> logger,
        TeamSportDataContext dataContext)
    {
        _logger = logger;
        _dataContext = dataContext;
    }

    public async Task<Result<Guid>> ExecuteAsync(
        CalculateFranchiseSeasonMetricsCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "CalculateFranchiseSeasonMetrics started. FranchiseSeasonId={FranchiseSeasonId}, SeasonYear={SeasonYear}",
            command.FranchiseSeasonId,
            command.SeasonYear);

        var competitionIds = await _dataContext.Contests
            .AsNoTracking()
            .Where(x => x.AwayTeamFranchiseSeasonId == command.FranchiseSeasonId ||
                        x.HomeTeamFranchiseSeasonId == command.FranchiseSeasonId)
            .SelectMany(x => x.Competitions.Select(c => c.Id))
            .ToListAsync(cancellationToken);

        var metrics = await _dataContext.CompetitionMetrics
            .AsNoTracking()
            .Where(cm => competitionIds.Contains(cm.CompetitionId) && cm.FranchiseSeasonId == command.FranchiseSeasonId)
            .ToListAsync(cancellationToken);

        if (!metrics.Any())
        {
            _logger.LogInformation(
                "No competition metrics found. FranchiseSeasonId={FranchiseSeasonId}, SeasonYear={SeasonYear}",
                command.FranchiseSeasonId,
                command.SeasonYear);
            return new Success<Guid>(command.FranchiseSeasonId);
        }

        var fsMetric = ComputeFranchiseSeasonMetric(metrics);
        fsMetric.FranchiseSeasonId = command.FranchiseSeasonId;
        fsMetric.Season = command.SeasonYear;

        var existingMetric = await _dataContext.FranchiseSeasonMetrics
            .FirstOrDefaultAsync(
                fsm => fsm.FranchiseSeasonId == command.FranchiseSeasonId && fsm.Season == command.SeasonYear,
                cancellationToken);

        if (existingMetric != null)
        {
            _dataContext.FranchiseSeasonMetrics.Remove(existingMetric);
        }

        await _dataContext.FranchiseSeasonMetrics.AddAsync(fsMetric, cancellationToken);
        await _dataContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "CalculateFranchiseSeasonMetrics completed. FranchiseSeasonId={FranchiseSeasonId}, SeasonYear={SeasonYear}, GamesPlayed={GamesPlayed}",
            command.FranchiseSeasonId,
            command.SeasonYear,
            fsMetric.GamesPlayed);

        return new Success<Guid>(command.FranchiseSeasonId);
    }

    private static FranchiseSeasonMetric ComputeFranchiseSeasonMetric(List<CompetitionMetric> metrics)
    {
        if (metrics == null || metrics.Count == 0)
            throw new ArgumentException("Cannot compute averages from an empty list.", nameof(metrics));

        decimal? SafeAvg(Func<CompetitionMetric, decimal?> selector) =>
            metrics.Select(selector).Where(v => v.HasValue).Select(v => v!.Value).DefaultIfEmpty().Average();

        return new FranchiseSeasonMetric
        {
            GamesPlayed = metrics.Count,

            // Offense
            ExplosiveRate = metrics.Average(m => m.ExplosiveRate),
            PointsPerDrive = metrics.Average(m => m.PointsPerDrive),
            RzScoreRate = SafeAvg(m => m.RzScoreRate),
            RzTdRate = SafeAvg(m => m.RzTdRate),
            SuccessRate = metrics.Average(m => m.SuccessRate),
            ThirdFourthRate = metrics.Average(m => m.ThirdFourthRate),
            TimePossRatio = metrics.Average(m => m.TimePossRatio),
            Ypp = metrics.Average(m => m.Ypp),

            // Defense
            OppExplosiveRate = metrics.Average(m => m.OppExplosiveRate),
            OppPointsPerDrive = metrics.Average(m => m.OppPointsPerDrive),
            OppRzTdRate = SafeAvg(m => m.OppRzTdRate),
            OppScoreTdRate = SafeAvg(m => m.OppScoreTdRate),
            OppSuccessRate = metrics.Average(m => m.OppSuccessRate),
            OppThirdFourthRate = metrics.Average(m => m.OppThirdFourthRate),
            OppYpp = metrics.Average(m => m.OppYpp),

            // ST / Discipline
            FgPctShrunk = metrics.Average(m => m.FgPctShrunk),
            FieldPosDiff = metrics.Average(m => m.FieldPosDiff),
            NetPunt = metrics.Average(m => m.NetPunt),
            PenaltyYardsPerPlay = metrics.Average(m => m.PenaltyYardsPerPlay),
            TurnoverMarginPerDrive = metrics.Average(m => m.TurnoverMarginPerDrive),

            ComputedUtc = DateTime.UtcNow
        };
    }
}
