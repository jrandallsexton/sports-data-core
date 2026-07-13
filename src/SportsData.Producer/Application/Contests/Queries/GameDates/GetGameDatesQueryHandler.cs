using Dapper;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Sql;

namespace SportsData.Producer.Application.Contests.Queries.GameDates;

public interface IGetGameDatesQueryHandler
{
    Task<Result<List<DateOnly>>> ExecuteAsync(
        GetGameDatesQuery query,
        CancellationToken cancellationToken = default);
}

public class GetGameDatesQueryHandler : IGetGameDatesQueryHandler
{
    private readonly TeamSportDataContext _dbContext;
    private readonly ProducerSqlQueryProvider _sqlProvider;

    public GetGameDatesQueryHandler(
        TeamSportDataContext dbContext,
        ProducerSqlQueryProvider sqlProvider)
    {
        _dbContext = dbContext;
        _sqlProvider = sqlProvider;
    }

    public async Task<Result<List<DateOnly>>> ExecuteAsync(
        GetGameDatesQuery query,
        CancellationToken cancellationToken = default)
    {
        var sql = _sqlProvider.GetGameDates();

        var connection = _dbContext.Database.GetDbConnection();
        var result = await connection.QueryAsync<DateOnly>(
            new CommandDefinition(
                sql,
                new { FromUtc = AsUtc(query.FromUtc), ToUtc = AsUtc(query.ToUtc) },
                cancellationToken: cancellationToken));

        return new Success<List<DateOnly>>(result.ToList());
    }

    // Contest.StartDateUtc is timestamptz; Npgsql rejects a DateTime with
    // Kind=Unspecified. Callers pass UTC instants, but normalize defensively:
    // Local → convert, Unspecified → treat as already-UTC.
    private static DateTime? AsUtc(DateTime? value)
    {
        if (value is not { } v)
            return null;

        return v.Kind switch
        {
            DateTimeKind.Utc => v,
            DateTimeKind.Local => v.ToUniversalTime(),
            _ => DateTime.SpecifyKind(v, DateTimeKind.Utc)
        };
    }
}
