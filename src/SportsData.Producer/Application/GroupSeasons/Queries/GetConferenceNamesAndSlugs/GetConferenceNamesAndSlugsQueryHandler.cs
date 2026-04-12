using Dapper;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Sql;

using System.Collections.Generic;
using System.Linq;

namespace SportsData.Producer.Application.GroupSeasons.Queries.GetConferenceNamesAndSlugs;

public interface IGetConferenceNamesAndSlugsQueryHandler
{
    Task<Result<List<ConferenceDivisionNameAndSlugDto>>> ExecuteAsync(
        GetConferenceNamesAndSlugsQuery query,
        CancellationToken cancellationToken = default);
}

public class GetConferenceNamesAndSlugsQueryHandler : IGetConferenceNamesAndSlugsQueryHandler
{
    private readonly TeamSportDataContext _dbContext;
    private readonly ProducerSqlQueryProvider _sqlProvider;

    public GetConferenceNamesAndSlugsQueryHandler(
        TeamSportDataContext dbContext,
        ProducerSqlQueryProvider sqlProvider)
    {
        _dbContext = dbContext;
        _sqlProvider = sqlProvider;
    }

    public async Task<Result<List<ConferenceDivisionNameAndSlugDto>>> ExecuteAsync(
        GetConferenceNamesAndSlugsQuery query,
        CancellationToken cancellationToken = default)
    {
        var sql = _sqlProvider.GetConferenceNamesAndSlugs();
        var connection = _dbContext.Database.GetDbConnection();

        var results = (await connection.QueryAsync<ConferenceDivisionNameAndSlugDto>(
            new CommandDefinition(
                sql,
                new { query.SeasonYear },
                cancellationToken: cancellationToken))).ToList();

        return new Success<List<ConferenceDivisionNameAndSlugDto>>(results);
    }
}
