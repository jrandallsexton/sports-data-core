using Dapper;

using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Sql;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Producer.Application.Contests.Queries.Matchups.GetMatchupForPreview;

public interface IGetMatchupForPreviewQueryHandler
{
    Task<Result<MatchupForPreviewDto>> ExecuteAsync(
        GetMatchupForPreviewQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<Dictionary<Guid, MatchupForPreviewDto>>> ExecuteBatchAsync(
        GetMatchupsForPreviewBatchQuery query,
        CancellationToken cancellationToken = default);
}

public class GetMatchupForPreviewQueryHandler : IGetMatchupForPreviewQueryHandler
{
    private readonly TeamSportDataContext _dbContext;
    private readonly ProducerSqlQueryProvider _sqlProvider;

    public GetMatchupForPreviewQueryHandler(
        TeamSportDataContext dbContext,
        ProducerSqlQueryProvider sqlProvider)
    {
        _dbContext = dbContext;
        _sqlProvider = sqlProvider;
    }

    public async Task<Result<MatchupForPreviewDto>> ExecuteAsync(
        GetMatchupForPreviewQuery query,
        CancellationToken cancellationToken = default)
    {
        var sql = _sqlProvider.GetMatchupForPreview();

        var connection = _dbContext.Database.GetDbConnection();
        var result = await connection.QueryFirstOrDefaultAsync<MatchupForPreviewDto>(
            new CommandDefinition(sql, new { query.ContestId }, cancellationToken: cancellationToken));

        if (result is null)
        {
            return new Failure<MatchupForPreviewDto>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure("ContestId", $"Matchup preview for contest {query.ContestId} not found")]);
        }

        return new Success<MatchupForPreviewDto>(result);
    }

    public async Task<Result<Dictionary<Guid, MatchupForPreviewDto>>> ExecuteBatchAsync(
        GetMatchupsForPreviewBatchQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.ContestIds.Length == 0)
        {
            return new Success<Dictionary<Guid, MatchupForPreviewDto>>(new Dictionary<Guid, MatchupForPreviewDto>());
        }

        var sql = _sqlProvider.GetMatchupForPreviewBatch();

        var connection = _dbContext.Database.GetDbConnection();
        var results = await connection.QueryAsync<MatchupForPreviewDto>(
            new CommandDefinition(sql, new { ContestIds = query.ContestIds }, cancellationToken: cancellationToken));

        var dict = results.ToDictionary(x => x.ContestId);
        return new Success<Dictionary<Guid, MatchupForPreviewDto>>(dict);
    }
}
