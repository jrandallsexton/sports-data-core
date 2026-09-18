using Dapper;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Sql;

namespace SportsData.Producer.Application.Contests.Queries.Matchups.GetEnteringRecordsByContestIds;

public interface IGetEnteringRecordsByContestIdsQueryHandler
{
    Task<Result<List<EnteringRecordDto>>> ExecuteAsync(
        GetEnteringRecordsByContestIdsQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Derives each contest's entering records from prior finalized outcomes.
/// Serves the API's matchup-record audit, which cannot reach this database.
/// </summary>
public class GetEnteringRecordsByContestIdsQueryHandler : IGetEnteringRecordsByContestIdsQueryHandler
{
    /// <summary>
    /// Caps a single request. The SQL runs two correlated laterals per
    /// contest, so cost is linear in the batch; 500 measured at well under a
    /// second against production-sized data and matches the audit job's page
    /// size on the calling side.
    /// </summary>
    public const int MaxContestIds = 500;

    private readonly ILogger<GetEnteringRecordsByContestIdsQueryHandler> _logger;
    private readonly TeamSportDataContext _dbContext;
    private readonly ProducerSqlQueryProvider _sqlProvider;

    public GetEnteringRecordsByContestIdsQueryHandler(
        ILogger<GetEnteringRecordsByContestIdsQueryHandler> logger,
        TeamSportDataContext dbContext,
        ProducerSqlQueryProvider sqlProvider)
    {
        _logger = logger;
        _dbContext = dbContext;
        _sqlProvider = sqlProvider;
    }

    public async Task<Result<List<EnteringRecordDto>>> ExecuteAsync(
        GetEnteringRecordsByContestIdsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.ContestIds.Length == 0)
        {
            return new Success<List<EnteringRecordDto>>([]);
        }

        if (query.ContestIds.Length > MaxContestIds)
        {
            return new Failure<List<EnteringRecordDto>>(
                default!,
                ResultStatus.Validation,
                [new FluentValidation.Results.ValidationFailure(
                    nameof(query.ContestIds),
                    $"At most {MaxContestIds} contest ids per request; received {query.ContestIds.Length}.")]);
        }

        var sql = _sqlProvider.GetEnteringRecordsByContestIds();

        var connection = _dbContext.Database.GetDbConnection();
        var result = await connection.QueryAsync<EnteringRecordDto>(
            new CommandDefinition(sql, new { ContestIds = query.ContestIds }, cancellationToken: cancellationToken));

        var records = result.ToList();

        // A requested contest missing from the result means it (or one of its
        // franchise seasons) could not be resolved here. The caller corrects
        // only what it receives, so silence would look like "nothing to fix".
        if (records.Count != query.ContestIds.Length)
        {
            _logger.LogWarning(
                "Entering records resolved for {Resolved} of {Requested} requested contests.",
                records.Count, query.ContestIds.Length);
        }

        return new Success<List<EnteringRecordDto>>(records);
    }
}
