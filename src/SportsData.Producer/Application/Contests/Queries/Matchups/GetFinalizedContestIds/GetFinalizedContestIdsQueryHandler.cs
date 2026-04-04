using Dapper;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Producer.Infrastructure.Data.Common;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Producer.Application.Contests.Queries.Matchups.GetFinalizedContestIds;

public interface IGetFinalizedContestIdsQueryHandler
{
    Task<Result<List<Guid>>> ExecuteAsync(
        GetFinalizedContestIdsQuery query,
        CancellationToken cancellationToken = default);
}

public class GetFinalizedContestIdsQueryHandler : IGetFinalizedContestIdsQueryHandler
{
    private readonly TeamSportDataContext _dbContext;

    public GetFinalizedContestIdsQueryHandler(TeamSportDataContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<List<Guid>>> ExecuteAsync(
        GetFinalizedContestIdsQuery query,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT "Id"
            FROM public."Contest"
            WHERE "FinalizedUtc" IS NOT NULL
              AND "SeasonWeekId" = @SeasonWeekId;
            """;

        var connection = _dbContext.Database.GetDbConnection();
        var result = await connection.QueryAsync<Guid>(
            new CommandDefinition(sql, new { query.SeasonWeekId }, cancellationToken: cancellationToken));

        return new Success<List<Guid>>(result.ToList());
    }
}
