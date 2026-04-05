using Dapper;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Sql;

using System;
using System.Collections.Generic;
using System.Linq;

namespace SportsData.Producer.Application.GroupSeasons.Queries.GetConferenceIdsBySlugs;

public interface IGetConferenceIdsBySlugsQueryHandler
{
    Task<Result<Dictionary<Guid, string>>> ExecuteAsync(
        GetConferenceIdsBySlugsQuery query,
        CancellationToken cancellationToken = default);
}

public class GetConferenceIdsBySlugsQueryHandler : IGetConferenceIdsBySlugsQueryHandler
{
    private readonly TeamSportDataContext _dbContext;
    private readonly ProducerSqlQueryProvider _sqlProvider;

    public GetConferenceIdsBySlugsQueryHandler(
        TeamSportDataContext dbContext,
        ProducerSqlQueryProvider sqlProvider)
    {
        _dbContext = dbContext;
        _sqlProvider = sqlProvider;
    }

    public async Task<Result<Dictionary<Guid, string>>> ExecuteAsync(
        GetConferenceIdsBySlugsQuery query,
        CancellationToken cancellationToken = default)
    {
        var sql = _sqlProvider.GetConferenceIdsBySlugs();
        var connection = _dbContext.Database.GetDbConnection();

        var results = await connection.QueryAsync<(Guid Id, string Slug)>(
            new CommandDefinition(
                sql,
                new { Slugs = query.Slugs.ToArray(), query.SeasonYear },
                cancellationToken: cancellationToken));

        var dict = results.ToDictionary(x => x.Id, x => x.Slug);
        return new Success<Dictionary<Guid, string>>(dict);
    }
}
