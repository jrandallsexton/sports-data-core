using Dapper;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Producer.Infrastructure.Data.Common;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Producer.Application.Contests.Queries.Matchups.GetCompletedFbsContestIds;

public interface IGetCompletedFbsContestIdsQueryHandler
{
    Task<Result<List<Guid>>> ExecuteAsync(
        GetCompletedFbsContestIdsQuery query,
        CancellationToken cancellationToken = default);
}

public class GetCompletedFbsContestIdsQueryHandler : IGetCompletedFbsContestIdsQueryHandler
{
    private readonly TeamSportDataContext _dbContext;

    public GetCompletedFbsContestIdsQueryHandler(TeamSportDataContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<List<Guid>>> ExecuteAsync(
        GetCompletedFbsContestIdsQuery query,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT c."Id"
            FROM public."Contest" c
            INNER JOIN public."FranchiseSeason" fsAway ON fsAway."Id" = c."AwayTeamFranchiseSeasonId"
            INNER JOIN public."FranchiseSeason" fsHome ON fsHome."Id" = c."HomeTeamFranchiseSeasonId"
            WHERE c."SeasonWeekId" = @SeasonWeekId
              AND c."FinalizedUtc" IS NOT NULL
              AND (fsAway."GroupSeasonMap" LIKE '%fbs%' OR fsHome."GroupSeasonMap" LIKE '%fbs%')
            ORDER BY c."Name";
            """;

        var connection = _dbContext.Database.GetDbConnection();
        var result = await connection.QueryAsync<Guid>(
            new CommandDefinition(sql, new { query.SeasonWeekId }, cancellationToken: cancellationToken));

        return new Success<List<Guid>>(result.ToList());
    }
}
