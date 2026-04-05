using Dapper;

using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.FranchiseSeasons.Queries.GetFranchiseSeasonStatistics;

public interface IGetFranchiseSeasonStatisticsQueryHandler
{
    Task<Result<FranchiseSeasonStatisticDto>> ExecuteAsync(
        GetFranchiseSeasonStatisticsQuery query,
        CancellationToken cancellationToken = default);
}

public class GetFranchiseSeasonStatisticsQueryHandler : IGetFranchiseSeasonStatisticsQueryHandler
{
    private readonly TeamSportDataContext _dbContext;

    public GetFranchiseSeasonStatisticsQueryHandler(TeamSportDataContext dbContext)
    {
        _dbContext = dbContext;
    }

    private const string Sql = """
        SELECT fssc."Name" AS "Category",
            fss."Name" AS "StatisticKey",
            fss."Name" AS "StatisticValue",
            fss."DisplayValue",
            fss."PerGameValue",
            fss."PerGameDisplayValue",
            fss."Rank"
        FROM public."FranchiseSeasonStatisticCategory" fssc
        INNER JOIN public."FranchiseSeasonStatistic" fss ON fss."FranchiseSeasonStatisticCategoryId" = fssc."Id"
        WHERE fssc."FranchiseSeasonId" = @FranchiseSeasonId
        ORDER BY "Category", "StatisticKey"
        """;

    public async Task<Result<FranchiseSeasonStatisticDto>> ExecuteAsync(
        GetFranchiseSeasonStatisticsQuery query,
        CancellationToken cancellationToken = default)
    {
        var connection = _dbContext.Database.GetDbConnection();

        var entries = (await connection.QueryAsync<FranchiseSeasonStatisticDto.FranchiseSeasonStatisticEntry>(
            new CommandDefinition(
                Sql,
                new { query.FranchiseSeasonId },
                cancellationToken: cancellationToken))).ToList();

        var dto = new FranchiseSeasonStatisticDto
        {
            GamesPlayed = 0,
            Statistics = entries
                .GroupBy(e => e.Category)
                .ToDictionary(
                    g => g.Key,
                    g => g.ToList())
        };

        return new Success<FranchiseSeasonStatisticDto>(dto);
    }
}
