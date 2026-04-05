using Dapper;

using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;

using System;

namespace SportsData.Producer.Application.FranchiseSeasons.Queries.GetFranchiseSeasonPreviewStats;

public interface IGetFranchiseSeasonPreviewStatsQueryHandler
{
    Task<Result<FranchiseSeasonModelStatsDto>> ExecuteAsync(
        GetFranchiseSeasonPreviewStatsQuery query,
        CancellationToken cancellationToken = default);
}

public class GetFranchiseSeasonPreviewStatsQueryHandler : IGetFranchiseSeasonPreviewStatsQueryHandler
{
    private readonly TeamSportDataContext _dbContext;
    private readonly ILogger<GetFranchiseSeasonPreviewStatsQueryHandler> _logger;

    public GetFranchiseSeasonPreviewStatsQueryHandler(
        TeamSportDataContext dbContext,
        ILogger<GetFranchiseSeasonPreviewStatsQueryHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    private const string Sql = """
        SELECT fssc."Name" AS "Category",
            fss."Name" AS "Statistic",
            fss."DisplayValue",
            fss."PerGameValue",
            fss."PerGameDisplayValue",
            fss."Rank"
        FROM public."FranchiseSeasonStatisticCategory" fssc
        INNER JOIN public."FranchiseSeasonStatistic" fss ON fss."FranchiseSeasonStatisticCategoryId" = fssc."Id"
        WHERE fssc."FranchiseSeasonId" = @FranchiseSeasonId
        ORDER BY "Category", "Statistic"
        """;

    public async Task<Result<FranchiseSeasonModelStatsDto>> ExecuteAsync(
        GetFranchiseSeasonPreviewStatsQuery query,
        CancellationToken cancellationToken = default)
    {
        var connection = _dbContext.Database.GetDbConnection();

        var rawStats = (await connection.QueryAsync<FranchiseSeasonRawStat>(
            new CommandDefinition(
                Sql,
                new { query.FranchiseSeasonId },
                cancellationToken: cancellationToken))).ToList();

        if (rawStats.Count == 0)
        {
            _logger.LogWarning(
                "Stats not found for FranchiseSeasonId={FranchiseSeasonId}",
                query.FranchiseSeasonId);

            return new Success<FranchiseSeasonModelStatsDto>(new FranchiseSeasonModelStatsDto());
        }

        var mapped = MapToModelStats(rawStats);
        return new Success<FranchiseSeasonModelStatsDto>(mapped);
    }

    private static FranchiseSeasonModelStatsDto MapToModelStats(List<FranchiseSeasonRawStat> stats)
    {
        var dict = stats
            .GroupBy(s => s.Statistic)
            .ToDictionary(g => g.Key, g => g.First());

        double? Get(string key) => dict.TryGetValue(key, out var s) ? s.PerGameValue : null;
        double? Div(double? a, double? b) => (a.HasValue && b.HasValue && b != 0) ? a / b : null;
        int? ToInt(double? val) => val.HasValue ? (int?)Convert.ToInt32(val.Value) : null;

        return new FranchiseSeasonModelStatsDto
        {
            PointsPerGame = Get("totalPointsPerGame"),
            YardsPerGame = Get("totalYardsFromScrimmage"),
            PassingYardsPerGame = Get("passingYards"),
            RushingYardsPerGame = Get("rushingYards"),
            ThirdDownConvPct = Get("thirdDownConvPct"),
            RedZoneScoringPct = Get("redzoneScoringPct"),
            TurnoverDifferential = Get("turnOverDifferential"),

            PenaltiesPerGame = Div(Get("totalPenalties"), Get("teamGamesPlayed")),
            PenaltyYardsPerGame = Div(Get("totalPenaltyYards"), Get("teamGamesPlayed")),
            AvgYardsPerPlay = Div(Get("totalYardsFromScrimmage"), Get("totalOffensivePlays")),

            Sacks = ToInt(Get("sacks")),
            Interceptions = ToInt(Get("interceptions")),
            FumblesLost = ToInt(Get("fumblesLost")),
            Takeaways = ToInt(Get("totalTakeaways"))
        };
    }
}
