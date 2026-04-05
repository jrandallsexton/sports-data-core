using Dapper;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;

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

    public GetConferenceNamesAndSlugsQueryHandler(TeamSportDataContext dbContext)
    {
        _dbContext = dbContext;
    }

    private const string Sql = """
        SELECT DISTINCT
            gsParent."Name" as "Division",
            gs."ShortName",
            gs."Slug"
        FROM public."GroupSeason" gs
        INNER JOIN public."GroupSeason" gsParent
            ON gsParent."Id" = gs."ParentId"
        WHERE gs."IsConference" = true
          AND gs."SeasonYear" = @SeasonYear
        ORDER BY gs."ShortName"
        """;

    public async Task<Result<List<ConferenceDivisionNameAndSlugDto>>> ExecuteAsync(
        GetConferenceNamesAndSlugsQuery query,
        CancellationToken cancellationToken = default)
    {
        var connection = _dbContext.Database.GetDbConnection();

        var results = (await connection.QueryAsync<ConferenceDivisionNameAndSlugDto>(
            new CommandDefinition(
                Sql,
                new { query.SeasonYear },
                cancellationToken: cancellationToken))).ToList();

        return new Success<List<ConferenceDivisionNameAndSlugDto>>(results);
    }
}
