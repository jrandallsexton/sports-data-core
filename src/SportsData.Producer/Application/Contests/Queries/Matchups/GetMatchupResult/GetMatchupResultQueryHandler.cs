using Dapper;

using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;

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

    public GetMatchupResultQueryHandler(TeamSportDataContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<MatchupResult>> ExecuteAsync(
        GetMatchupResultQuery query,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
              c."Id" AS "ContestId",
              c."AwayTeamFranchiseSeasonId" AS "AwayFranchiseSeasonId",
              c."HomeTeamFranchiseSeasonId" AS "HomeFranchiseSeasonId",
              c."SeasonWeekId",
              coo."Spread" AS "Spread",
              c."AwayScore",
              c."HomeScore",
              c."WinnerFranchiseId" AS "WinnerFranchiseSeasonId",
              c."SpreadWinnerFranchiseId" AS "SpreadWinnerFranchiseSeasonId",
              c."FinalizedUtc"
            FROM public."Contest" c
            INNER JOIN public."Competition" co ON co."ContestId" = c."Id"
            LEFT JOIN LATERAL (
              SELECT *
              FROM public."CompetitionOdds"
              WHERE "CompetitionId" = co."Id"
                AND "ProviderId" IN ('58', '100')
              ORDER BY CASE WHEN "ProviderId" = '58' THEN 1 ELSE 2 END
              LIMIT 1
            ) coo ON TRUE
            WHERE c."Id" = @ContestId
            """;

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
