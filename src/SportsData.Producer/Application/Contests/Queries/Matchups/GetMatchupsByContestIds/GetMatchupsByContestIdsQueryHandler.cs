using Dapper;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Sql;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Producer.Application.Contests.Queries.Matchups.GetMatchupsByContestIds;

public interface IGetMatchupsByContestIdsQueryHandler
{
    Task<Result<List<LeagueMatchupDto>>> ExecuteAsync(
        GetMatchupsByContestIdsQuery query,
        CancellationToken cancellationToken = default);
}

public class GetMatchupsByContestIdsQueryHandler : IGetMatchupsByContestIdsQueryHandler
{
    private readonly TeamSportDataContext _dbContext;
    private readonly ProducerSqlQueryProvider _sqlProvider;

    public GetMatchupsByContestIdsQueryHandler(
        TeamSportDataContext dbContext,
        ProducerSqlQueryProvider sqlProvider)
    {
        _dbContext = dbContext;
        _sqlProvider = sqlProvider;
    }

    public async Task<Result<List<LeagueMatchupDto>>> ExecuteAsync(
        GetMatchupsByContestIdsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.ContestIds.Length == 0)
        {
            return new Success<List<LeagueMatchupDto>>(new List<LeagueMatchupDto>());
        }

        var sql = _sqlProvider.GetMatchupsByContestIds();

        var connection = _dbContext.Database.GetDbConnection();
        var result = await connection.QueryAsync<LeagueMatchupDto>(
            new CommandDefinition(sql, new { ContestIds = query.ContestIds }, cancellationToken: cancellationToken));

        return new Success<List<LeagueMatchupDto>>(result.ToList());
    }
}
