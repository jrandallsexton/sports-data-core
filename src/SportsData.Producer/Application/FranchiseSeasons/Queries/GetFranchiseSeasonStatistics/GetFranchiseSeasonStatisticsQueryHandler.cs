using Dapper;

using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Sql;

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
    private readonly ProducerSqlQueryProvider _sqlProvider;

    public GetFranchiseSeasonStatisticsQueryHandler(
        TeamSportDataContext dbContext,
        ProducerSqlQueryProvider sqlProvider)
    {
        _dbContext = dbContext;
        _sqlProvider = sqlProvider;
    }

    public async Task<Result<FranchiseSeasonStatisticDto>> ExecuteAsync(
        GetFranchiseSeasonStatisticsQuery query,
        CancellationToken cancellationToken = default)
    {
        var sql = _sqlProvider.GetFranchiseSeasonStatistics();
        var connection = _dbContext.Database.GetDbConnection();

        var entries = (await connection.QueryAsync<FranchiseSeasonStatisticDto.FranchiseSeasonStatisticEntry>(
            new CommandDefinition(
                sql,
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
