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

namespace SportsData.Producer.Application.Contests.Queries.Matchups.GetMatchupsForSeasonWeek;

public interface IGetMatchupsForSeasonWeekQueryHandler
{
    Task<Result<List<Matchup>>> ExecuteAsync(
        GetMatchupsForSeasonWeekQuery query,
        CancellationToken cancellationToken = default);
}

public class GetMatchupsForSeasonWeekQueryHandler : IGetMatchupsForSeasonWeekQueryHandler
{
    private readonly TeamSportDataContext _dbContext;
    private readonly ProducerSqlQueryProvider _sqlProvider;

    public GetMatchupsForSeasonWeekQueryHandler(
        TeamSportDataContext dbContext,
        ProducerSqlQueryProvider sqlProvider)
    {
        _dbContext = dbContext;
        _sqlProvider = sqlProvider;
    }

    public async Task<Result<List<Matchup>>> ExecuteAsync(
        GetMatchupsForSeasonWeekQuery query,
        CancellationToken cancellationToken = default)
    {
        var sql = _sqlProvider.GetMatchupsForSeasonWeek();

        var connection = _dbContext.Database.GetDbConnection();
        var result = await connection.QueryAsync<Matchup>(
            new CommandDefinition(sql, new { query.SeasonYear, query.SeasonWeekNumber }, cancellationToken: cancellationToken));

        return new Success<List<Matchup>>(result.ToList());
    }
}
