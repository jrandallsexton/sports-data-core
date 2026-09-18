using Dapper;

using FluentValidation;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Sql;

namespace SportsData.Producer.Application.Contests.Queries.GetEnteringRecordsByContestIds;

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
    private readonly ILogger<GetEnteringRecordsByContestIdsQueryHandler> _logger;
    private readonly TeamSportDataContext _dbContext;
    private readonly ProducerSqlQueryProvider _sqlProvider;
    private readonly IValidator<GetEnteringRecordsByContestIdsQuery> _validator;

    public GetEnteringRecordsByContestIdsQueryHandler(
        ILogger<GetEnteringRecordsByContestIdsQueryHandler> logger,
        TeamSportDataContext dbContext,
        ProducerSqlQueryProvider sqlProvider,
        IValidator<GetEnteringRecordsByContestIdsQuery> validator)
    {
        _logger = logger;
        _dbContext = dbContext;
        _sqlProvider = sqlProvider;
        _validator = validator;
    }

    public async Task<Result<List<EnteringRecordDto>>> ExecuteAsync(
        GetEnteringRecordsByContestIdsQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return new Failure<List<EnteringRecordDto>>(
                default!,
                ResultStatus.Validation,
                validation.Errors);
        }

        // Empty is a legitimate no-op, not a validation failure.
        if (query.ContestIds.Length == 0)
        {
            return new Success<List<EnteringRecordDto>>([]);
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
