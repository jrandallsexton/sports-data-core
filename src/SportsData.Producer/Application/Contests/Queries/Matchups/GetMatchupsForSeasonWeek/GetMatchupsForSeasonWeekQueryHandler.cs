using Dapper;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;

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

    public GetMatchupsForSeasonWeekQueryHandler(TeamSportDataContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<List<Matchup>>> ExecuteAsync(
        GetMatchupsForSeasonWeekQuery query,
        CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT
            {MatchupSqlBuilder.MatchupSelectColumns}
            FROM public."Contest" c
            {MatchupSqlBuilder.MatchupJoins}
            {MatchupSqlBuilder.RankingJoinsBySeasonWeek}
            WHERE s."Year" = @SeasonYear AND sw."Number" = @SeasonWeekNumber
            {MatchupSqlBuilder.MatchupOrderBy}
            """;

        var connection = _dbContext.Database.GetDbConnection();
        var result = await connection.QueryAsync<Matchup>(
            new CommandDefinition(sql, new { query.SeasonYear, query.SeasonWeekNumber }, cancellationToken: cancellationToken));

        return new Success<List<Matchup>>(result.ToList());
    }
}
