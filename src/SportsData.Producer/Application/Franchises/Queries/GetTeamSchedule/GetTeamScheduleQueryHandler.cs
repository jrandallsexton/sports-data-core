using Dapper;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Sql;

namespace SportsData.Producer.Application.Franchises.Queries.GetTeamSchedule;

public interface IGetTeamScheduleQueryHandler
{
    Task<Result<List<TeamCardScheduleItemDto>>> ExecuteAsync(
        GetTeamScheduleQuery query,
        CancellationToken cancellationToken = default);
}

public class GetTeamScheduleQueryHandler : IGetTeamScheduleQueryHandler
{
    private readonly TeamSportDataContext _dbContext;
    private readonly ProducerSqlQueryProvider _sqlProvider;

    public GetTeamScheduleQueryHandler(
        TeamSportDataContext dbContext,
        ProducerSqlQueryProvider sqlProvider)
    {
        _dbContext = dbContext;
        _sqlProvider = sqlProvider;
    }

    public async Task<Result<List<TeamCardScheduleItemDto>>> ExecuteAsync(
        GetTeamScheduleQuery query,
        CancellationToken cancellationToken = default)
    {
        var connection = _dbContext.Database.GetDbConnection();
        var parameters = new { query.Slug, query.SeasonYear, query.AsOfDate };

        var schedule = (await connection.QueryAsync<TeamCardScheduleItemDto>(
            new CommandDefinition(
                _sqlProvider.GetTeamScheduleCompleted(),
                parameters,
                cancellationToken: cancellationToken))).ToList();

        return new Success<List<TeamCardScheduleItemDto>>(schedule);
    }
}
