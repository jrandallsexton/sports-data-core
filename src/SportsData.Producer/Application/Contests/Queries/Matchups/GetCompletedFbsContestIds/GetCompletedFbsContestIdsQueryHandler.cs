using Dapper;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Sql;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Producer.Application.Contests.Queries.Matchups.GetCompletedFbsContestIds;

public interface IGetCompletedFbsContestIdsQueryHandler
{
    Task<Result<List<Guid>>> ExecuteAsync(
        GetCompletedFbsContestIdsQuery query,
        CancellationToken cancellationToken = default);
}

public class GetCompletedFbsContestIdsQueryHandler : IGetCompletedFbsContestIdsQueryHandler
{
    private readonly TeamSportDataContext _dbContext;
    private readonly ProducerSqlQueryProvider _sqlProvider;

    public GetCompletedFbsContestIdsQueryHandler(
        TeamSportDataContext dbContext,
        ProducerSqlQueryProvider sqlProvider)
    {
        _dbContext = dbContext;
        _sqlProvider = sqlProvider;
    }

    public async Task<Result<List<Guid>>> ExecuteAsync(
        GetCompletedFbsContestIdsQuery query,
        CancellationToken cancellationToken = default)
    {
        var sql = _sqlProvider.GetCompletedFbsContestIds();

        var connection = _dbContext.Database.GetDbConnection();
        var result = await connection.QueryAsync<Guid>(
            new CommandDefinition(sql, new { query.SeasonWeekId }, cancellationToken: cancellationToken));

        return new Success<List<Guid>>(result.ToList());
    }
}
