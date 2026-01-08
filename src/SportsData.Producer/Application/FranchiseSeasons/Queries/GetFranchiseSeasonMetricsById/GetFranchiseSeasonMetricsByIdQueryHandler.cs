using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.FranchiseSeasons.Queries.GetFranchiseSeasonMetricsById;

public interface IGetFranchiseSeasonMetricsByIdQueryHandler
{
    Task<Result<FranchiseSeasonMetricsDto>> ExecuteAsync(
        GetFranchiseSeasonMetricsByIdQuery query,
        CancellationToken cancellationToken = default);
}

public class GetFranchiseSeasonMetricsByIdQueryHandler : IGetFranchiseSeasonMetricsByIdQueryHandler
{
    private readonly TeamSportDataContext _dataContext;
    private readonly ILogger<GetFranchiseSeasonMetricsByIdQueryHandler> _logger;

    public GetFranchiseSeasonMetricsByIdQueryHandler(
        TeamSportDataContext dataContext,
        ILogger<GetFranchiseSeasonMetricsByIdQueryHandler> logger)
    {
        _dataContext = dataContext;
        _logger = logger;
    }

    public async Task<Result<FranchiseSeasonMetricsDto>> ExecuteAsync(
        GetFranchiseSeasonMetricsByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "GetFranchiseSeasonMetricsById started. FranchiseSeasonId={FranchiseSeasonId}",
            query.FranchiseSeasonId);

        var metric = await _dataContext.FranchiseSeasonMetrics
            .Include(fsm => fsm.FranchiseSeason)
                .ThenInclude(fs => fs.Franchise)
            .Include(fsm => fsm.FranchiseSeason)
                .ThenInclude(fs => fs.GroupSeason)
            .AsNoTracking()
            .FirstOrDefaultAsync(fsm => fsm.FranchiseSeasonId == query.FranchiseSeasonId, cancellationToken);

        if (metric == null)
        {
            _logger.LogError(
                "No FranchiseSeasonMetric found. FranchiseSeasonId={FranchiseSeasonId}",
                query.FranchiseSeasonId);

            return new Failure<FranchiseSeasonMetricsDto>(
                value: default!,
                status: ResultStatus.NotFound,
                errors:
                [
                    new ValidationFailure(nameof(query.FranchiseSeasonId), "No metrics found for this franchise season.")
                ]);
        }

        var dto = new FranchiseSeasonMetricsDto()
        {
            Conference = metric.FranchiseSeason.GroupSeason?.Slug,
            ExplosiveRate = metric.ExplosiveRate,
            FgPctShrunk = metric.FgPctShrunk,
            FieldPosDiff = metric.FieldPosDiff,
            FranchiseName = metric.FranchiseSeason.Franchise.DisplayNameShort,
            FranchiseSlug = metric.FranchiseSeason.Franchise.Slug,
            GamesPlayed = metric.GamesPlayed,
            NetPunt = metric.NetPunt,
            OppExplosiveRate = metric.OppExplosiveRate,
            OppPointsPerDrive = metric.OppPointsPerDrive,
            OppRzTdRate = metric.OppRzTdRate,
            OppSuccessRate = metric.OppSuccessRate,
            OppThirdFourthRate = metric.OppThirdFourthRate,
            OppYpp = metric.OppYpp,
            PenaltyYardsPerPlay = metric.PenaltyYardsPerPlay,
            PointsPerDrive = metric.PointsPerDrive,
            RzScoreRate = metric.RzScoreRate,
            RzTdRate = metric.RzTdRate,
            SeasonYear = metric.Season,
            SuccessRate = metric.SuccessRate,
            ThirdFourthRate = metric.ThirdFourthRate,
            TimePossRatio = metric.TimePossRatio,
            TurnoverMarginPerDrive = metric.TurnoverMarginPerDrive,
            Ypp = metric.Ypp
        };

        _logger.LogInformation(
            "GetFranchiseSeasonMetricsById completed. FranchiseSeasonId={FranchiseSeasonId}",
            query.FranchiseSeasonId);

        return new Success<FranchiseSeasonMetricsDto>(dto);
    }
}
