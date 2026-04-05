using Dapper;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Sql;

using System.Collections.Generic;
using System.Linq;

namespace SportsData.Producer.Application.FranchiseSeasons.Queries.GetFranchiseSeasonCompetitionResults;

public interface IGetFranchiseSeasonCompetitionResultsQueryHandler
{
    Task<Result<List<FranchiseSeasonCompetitionResultDto>>> ExecuteAsync(
        GetFranchiseSeasonCompetitionResultsQuery query,
        CancellationToken cancellationToken = default);
}

public class GetFranchiseSeasonCompetitionResultsQueryHandler : IGetFranchiseSeasonCompetitionResultsQueryHandler
{
    private readonly TeamSportDataContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ProducerSqlQueryProvider _sqlProvider;

    public GetFranchiseSeasonCompetitionResultsQueryHandler(
        TeamSportDataContext dbContext,
        IDateTimeProvider dateTimeProvider,
        ProducerSqlQueryProvider sqlProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _sqlProvider = sqlProvider;
    }

    public async Task<Result<List<FranchiseSeasonCompetitionResultDto>>> ExecuteAsync(
        GetFranchiseSeasonCompetitionResultsQuery query,
        CancellationToken cancellationToken = default)
    {
        var sql = _sqlProvider.GetFranchiseSeasonCompetitionResults();
        var connection = _dbContext.Database.GetDbConnection();

        var results = (await connection.QueryAsync<FranchiseSeasonCompetitionResultDto>(
            new CommandDefinition(
                sql,
                new { query.FranchiseSeasonId, NowUtc = _dateTimeProvider.UtcNow() },
                cancellationToken: cancellationToken))).ToList();

        return new Success<List<FranchiseSeasonCompetitionResultDto>>(results);
    }
}
