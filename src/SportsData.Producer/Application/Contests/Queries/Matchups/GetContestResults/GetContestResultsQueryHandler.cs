using Dapper;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Sql;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Producer.Application.Contests.Queries.Matchups.GetContestResults;

public interface IGetContestResultsByContestIdsQueryHandler
{
    Task<Result<List<ContestResultDto>>> ExecuteAsync(
        GetContestResultsByContestIdsQuery query,
        CancellationToken cancellationToken = default);
}

public class GetContestResultsByContestIdsQueryHandler : IGetContestResultsByContestIdsQueryHandler
{
    private readonly TeamSportDataContext _dbContext;
    private readonly ProducerSqlQueryProvider _sqlProvider;

    public GetContestResultsByContestIdsQueryHandler(
        TeamSportDataContext dbContext,
        ProducerSqlQueryProvider sqlProvider)
    {
        _dbContext = dbContext;
        _sqlProvider = sqlProvider;
    }

    public async Task<Result<List<ContestResultDto>>> ExecuteAsync(
        GetContestResultsByContestIdsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.ContestIds.Length == 0)
        {
            return new Success<List<ContestResultDto>>(new List<ContestResultDto>());
        }

        var sql = _sqlProvider.GetContestResultsByContestIds();

        var connection = _dbContext.Database.GetDbConnection();
        var result = await connection.QueryAsync<ContestResultDto>(
            new CommandDefinition(sql, new { ContestIds = query.ContestIds }, cancellationToken: cancellationToken));

        return new Success<List<ContestResultDto>>(result.ToList());
    }
}
