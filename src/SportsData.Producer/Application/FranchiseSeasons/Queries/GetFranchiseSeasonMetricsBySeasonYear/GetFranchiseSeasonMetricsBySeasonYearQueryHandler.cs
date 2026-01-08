using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.FranchiseSeasons.Queries.GetFranchiseSeasonMetricsBySeasonYear;

public interface IGetFranchiseSeasonMetricsBySeasonYearQueryHandler
{
    Task<Result<List<FranchiseSeasonMetricsDto>>> ExecuteAsync(
        GetFranchiseSeasonMetricsBySeasonYearQuery query,
        CancellationToken cancellationToken = default);
}

public class GetFranchiseSeasonMetricsBySeasonYearQueryHandler : IGetFranchiseSeasonMetricsBySeasonYearQueryHandler
{
    private readonly TeamSportDataContext _dataContext;
    private readonly ILogger<GetFranchiseSeasonMetricsBySeasonYearQueryHandler> _logger;

    public GetFranchiseSeasonMetricsBySeasonYearQueryHandler(
        TeamSportDataContext dataContext,
        ILogger<GetFranchiseSeasonMetricsBySeasonYearQueryHandler> logger)
    {
        _dataContext = dataContext;
        _logger = logger;
    }

    public async Task<Result<List<FranchiseSeasonMetricsDto>>> ExecuteAsync(
        GetFranchiseSeasonMetricsBySeasonYearQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "GetFranchiseSeasonMetricsBySeasonYear started. SeasonYear={SeasonYear}",
            query.SeasonYear);

        var metrics = await _dataContext.FranchiseSeasonMetrics
            .Include(fsm => fsm.FranchiseSeason)
                .ThenInclude(fs => fs.Franchise)
            .Include(fsm => fsm.FranchiseSeason)
                .ThenInclude(fs => fs.GroupSeason)
            .Where(fsm => fsm.Season == query.SeasonYear)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var dtos = metrics.Select(fsm => new FranchiseSeasonMetricsDto()
        {
            Conference = fsm.FranchiseSeason.GroupSeason?.Slug,
            ExplosiveRate = fsm.ExplosiveRate,
            FgPctShrunk = fsm.FgPctShrunk,
            FieldPosDiff = fsm.FieldPosDiff,
            FranchiseName = fsm.FranchiseSeason.Franchise.DisplayNameShort,
            FranchiseSlug = fsm.FranchiseSeason.Franchise.Slug,
            GamesPlayed = fsm.GamesPlayed,
            NetPunt = fsm.NetPunt,
            OppExplosiveRate = fsm.OppExplosiveRate,
            OppPointsPerDrive = fsm.OppPointsPerDrive,
            OppRzTdRate = fsm.OppRzTdRate,
            OppSuccessRate = fsm.OppSuccessRate,
            OppThirdFourthRate = fsm.OppThirdFourthRate,
            OppYpp = fsm.OppYpp,
            PenaltyYardsPerPlay = fsm.PenaltyYardsPerPlay,
            PointsPerDrive = fsm.PointsPerDrive,
            RzScoreRate = fsm.RzScoreRate,
            RzTdRate = fsm.RzTdRate,
            SeasonYear = query.SeasonYear,
            SuccessRate = fsm.SuccessRate,
            ThirdFourthRate = fsm.ThirdFourthRate,
            TimePossRatio = fsm.TimePossRatio,
            TurnoverMarginPerDrive = fsm.TurnoverMarginPerDrive,
            Ypp = fsm.Ypp,

            // scoring summary fields
            PtsScoredMin = fsm.FranchiseSeason.PtsScoredMin,
            PtsScoredMax = fsm.FranchiseSeason.PtsScoredMax,
            PtsScoredAvg = fsm.FranchiseSeason.PtsScoredAvg,

            PtsAllowedMin = fsm.FranchiseSeason.PtsAllowedMin,
            PtsAllowedMax = fsm.FranchiseSeason.PtsAllowedMax,
            PtsAllowedAvg = fsm.FranchiseSeason.PtsAllowedAvg,

            MarginWinMin = fsm.FranchiseSeason.MarginWinMin,
            MarginWinMax = fsm.FranchiseSeason.MarginWinMax,
            MarginWinAvg = fsm.FranchiseSeason.MarginWinAvg,

            MarginLossMin = fsm.FranchiseSeason.MarginLossMin,
            MarginLossMax = fsm.FranchiseSeason.MarginLossMax,
            MarginLossAvg = fsm.FranchiseSeason.MarginLossAvg
        }).ToList();

        _logger.LogInformation(
            "GetFranchiseSeasonMetricsBySeasonYear completed. SeasonYear={SeasonYear}, Count={Count}",
            query.SeasonYear,
            dtos.Count);

        return new Success<List<FranchiseSeasonMetricsDto>>(dtos);
    }
}
