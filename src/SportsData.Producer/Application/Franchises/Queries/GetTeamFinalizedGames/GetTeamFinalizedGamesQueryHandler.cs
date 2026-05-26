using Dapper;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Sql;

namespace SportsData.Producer.Application.Franchises.Queries.GetTeamFinalizedGames;

public interface IGetTeamFinalizedGamesQueryHandler
{
    Task<Result<List<TeamCardScheduleItemDto>>> ExecuteAsync(
        GetTeamFinalizedGamesQuery query,
        CancellationToken cancellationToken = default);
}

public class GetTeamFinalizedGamesQueryHandler : IGetTeamFinalizedGamesQueryHandler
{
    private readonly TeamSportDataContext _dbContext;
    private readonly ProducerSqlQueryProvider _sqlProvider;

    public GetTeamFinalizedGamesQueryHandler(
        TeamSportDataContext dbContext,
        ProducerSqlQueryProvider sqlProvider)
    {
        _dbContext = dbContext;
        _sqlProvider = sqlProvider;
    }

    public async Task<Result<List<TeamCardScheduleItemDto>>> ExecuteAsync(
        GetTeamFinalizedGamesQuery query,
        CancellationToken cancellationToken = default)
    {
        var connection = _dbContext.Database.GetDbConnection();
        var parameters = new { query.Slug, query.SeasonYear, query.AsOfDate };

        var games = (await connection.QueryAsync<TeamCardScheduleItemDto>(
            new CommandDefinition(
                _sqlProvider.GetTeamFinalizedGames(),
                parameters,
                cancellationToken: cancellationToken))).ToList();

        return new Success<List<TeamCardScheduleItemDto>>(games);
    }
}
