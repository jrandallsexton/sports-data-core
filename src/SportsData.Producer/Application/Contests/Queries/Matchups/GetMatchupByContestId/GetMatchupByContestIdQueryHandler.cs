using Dapper;

using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Sql;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Producer.Application.Contests.Queries.Matchups.GetMatchupByContestId;

public interface IGetMatchupByContestIdQueryHandler
{
    Task<Result<Matchup>> ExecuteAsync(
        GetMatchupByContestIdQuery query,
        CancellationToken cancellationToken = default);
}

public class GetMatchupByContestIdQueryHandler : IGetMatchupByContestIdQueryHandler
{
    private readonly TeamSportDataContext _dbContext;
    private readonly ProducerSqlQueryProvider _sqlProvider;

    public GetMatchupByContestIdQueryHandler(
        TeamSportDataContext dbContext,
        ProducerSqlQueryProvider sqlProvider)
    {
        _dbContext = dbContext;
        _sqlProvider = sqlProvider;
    }

    public async Task<Result<Matchup>> ExecuteAsync(
        GetMatchupByContestIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var sql = _sqlProvider.GetMatchupByContestId();

        var connection = _dbContext.Database.GetDbConnection();
        var result = await connection.QueryFirstOrDefaultAsync<Matchup>(
            new CommandDefinition(sql, new { query.ContestId }, cancellationToken: cancellationToken));

        if (result is null)
        {
            return new Failure<Matchup>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure("ContestId", $"Matchup for contest {query.ContestId} not found")]);
        }

        return new Success<Matchup>(result);
    }
}
