using Dapper;

using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;

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

    public GetMatchupByContestIdQueryHandler(TeamSportDataContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<Matchup>> ExecuteAsync(
        GetMatchupByContestIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT
            {MatchupSqlBuilder.MatchupSelectColumns}
            FROM public."Contest" c
            {MatchupSqlBuilder.MatchupJoins}
            {MatchupSqlBuilder.RankingJoinsBySeasonWeek}
            WHERE c."Id" = @ContestId
            """;

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
