using Dapper;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Sql;

using System.Linq;

namespace SportsData.Producer.Application.Franchises.Queries.GetTeamRoster;

public interface IGetTeamRosterQueryHandler
{
    Task<Result<TeamRosterDto>> ExecuteAsync(
        GetTeamRosterQuery query,
        CancellationToken cancellationToken = default);
}

public class GetTeamRosterQueryHandler : IGetTeamRosterQueryHandler
{
    private readonly TeamSportDataContext _dbContext;
    private readonly ProducerSqlQueryProvider _sqlProvider;

    public GetTeamRosterQueryHandler(
        TeamSportDataContext dbContext,
        ProducerSqlQueryProvider sqlProvider)
    {
        _dbContext = dbContext;
        _sqlProvider = sqlProvider;
    }

    public async Task<Result<TeamRosterDto>> ExecuteAsync(
        GetTeamRosterQuery query,
        CancellationToken cancellationToken = default)
    {
        var connection = _dbContext.Database.GetDbConnection();
        var parameters = new { query.Slug, query.SeasonYear };

        var players = (await connection.QueryAsync<TeamRosterEntryDto>(
            new CommandDefinition(_sqlProvider.GetTeamRoster(), parameters, cancellationToken: cancellationToken))).ToList();

        var result = new TeamRosterDto
        {
            Players = players
        };

        return new Success<TeamRosterDto>(result);
    }
}
