using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Entities.Metrics;

namespace SportsData.Producer.Application.Contests.Queries.GetContestOverview;

public static class MetricExtensions
{
    public static CompetitionMetricDto ToDto(this CompetitionMetric metric) => new()
    {
        CompetitionId = metric.CompetitionId,
        FranchiseSeasonId = metric.FranchiseSeasonId,
        Season = metric.Season,
        Ypp = metric.Ypp,
        SuccessRate = metric.SuccessRate,
        ExplosiveRate = metric.ExplosiveRate,
        PointsPerDrive = metric.PointsPerDrive,
        ThirdFourthRate = metric.ThirdFourthRate,
        RzTdRate = metric.RzTdRate,
        RzScoreRate = metric.RzScoreRate,
        TimePossRatio = metric.TimePossRatio,
        OppYpp = metric.OppYpp,
        OppSuccessRate = metric.OppSuccessRate,
        OppExplosiveRate = metric.OppExplosiveRate,
        OppPointsPerDrive = metric.OppPointsPerDrive,
        OppThirdFourthRate = metric.OppThirdFourthRate,
        OppRzTdRate = metric.OppRzTdRate,
        OppScoreTdRate = metric.OppScoreTdRate,
        NetPunt = metric.NetPunt,
        FgPctShrunk = metric.FgPctShrunk,
        FieldPosDiff = metric.FieldPosDiff,
        TurnoverMarginPerDrive = metric.TurnoverMarginPerDrive,
        PenaltyYardsPerPlay = metric.PenaltyYardsPerPlay
    };
}