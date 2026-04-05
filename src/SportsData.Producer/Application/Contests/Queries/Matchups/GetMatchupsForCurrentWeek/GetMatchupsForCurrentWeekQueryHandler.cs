using Dapper;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Sql;

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Producer.Application.Contests.Queries.Matchups.GetMatchupsForCurrentWeek;

public interface IGetMatchupsForCurrentWeekQueryHandler
{
    Task<Result<List<Matchup>>> ExecuteAsync(
        GetMatchupsForCurrentWeekQuery query,
        CancellationToken cancellationToken = default);
}

public class GetMatchupsForCurrentWeekQueryHandler : IGetMatchupsForCurrentWeekQueryHandler
{
    private readonly TeamSportDataContext _dbContext;
    private readonly ProducerSqlQueryProvider _sqlProvider;

    public GetMatchupsForCurrentWeekQueryHandler(
        TeamSportDataContext dbContext,
        ProducerSqlQueryProvider sqlProvider)
    {
        _dbContext = dbContext;
        _sqlProvider = sqlProvider;
    }

    public async Task<Result<List<Matchup>>> ExecuteAsync(
        GetMatchupsForCurrentWeekQuery query,
        CancellationToken cancellationToken = default)
    {
        var sql = _sqlProvider.GetMatchupsForCurrentWeek();

        var connection = _dbContext.Database.GetDbConnection();
        var result = await connection.QueryAsync<Matchup>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));

        return new Success<List<Matchup>>(result.ToList());
    }
}
