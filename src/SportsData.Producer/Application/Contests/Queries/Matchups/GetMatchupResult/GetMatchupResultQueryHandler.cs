using Dapper;

using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Sql;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Producer.Application.Contests.Queries.Matchups.GetMatchupResult;

public interface IGetMatchupResultQueryHandler
{
    Task<Result<MatchupResult>> ExecuteAsync(
        GetMatchupResultQuery query,
        CancellationToken cancellationToken = default);
}

public class GetMatchupResultQueryHandler : IGetMatchupResultQueryHandler
{
    private readonly TeamSportDataContext _dbContext;
    private readonly ProducerSqlQueryProvider _sqlProvider;

    public GetMatchupResultQueryHandler(
        TeamSportDataContext dbContext,
        ProducerSqlQueryProvider sqlProvider)
    {
        _dbContext = dbContext;
        _sqlProvider = sqlProvider;
    }

    public async Task<Result<MatchupResult>> ExecuteAsync(
        GetMatchupResultQuery query,
        CancellationToken cancellationToken = default)
    {
        var sql = _sqlProvider.GetMatchupResultByContestId();

        var connection = _dbContext.Database.GetDbConnection();
        var result = await connection.QueryFirstOrDefaultAsync<MatchupResult>(
            new CommandDefinition(sql, new { query.ContestId }, cancellationToken: cancellationToken));

        if (result is null)
        {
            return new Failure<MatchupResult>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure("ContestId", $"Contest {query.ContestId} not found")]);
        }

        return new Success<MatchupResult>(result);
    }
}
