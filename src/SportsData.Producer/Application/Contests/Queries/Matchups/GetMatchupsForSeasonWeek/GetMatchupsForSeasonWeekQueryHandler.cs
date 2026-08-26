using Dapper;

using FluentValidation;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Sql;

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Producer.Application.Contests.Queries.Matchups.GetMatchupsForSeasonWeek;

public interface IGetMatchupsForSeasonWeekQueryHandler
{
    Task<Result<List<Matchup>>> ExecuteAsync(
        GetMatchupsForSeasonWeekQuery query,
        CancellationToken cancellationToken = default);
}

public class GetMatchupsForSeasonWeekQueryHandler : IGetMatchupsForSeasonWeekQueryHandler
{
    private readonly TeamSportDataContext _dbContext;
    private readonly ProducerSqlQueryProvider _sqlProvider;
    private readonly IValidator<GetMatchupsForSeasonWeekQuery> _validator;

    public GetMatchupsForSeasonWeekQueryHandler(
        TeamSportDataContext dbContext,
        ProducerSqlQueryProvider sqlProvider,
        IValidator<GetMatchupsForSeasonWeekQuery> validator)
    {
        _dbContext = dbContext;
        _sqlProvider = sqlProvider;
        _validator = validator;
    }

    public async Task<Result<List<Matchup>>> ExecuteAsync(
        GetMatchupsForSeasonWeekQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return new Failure<List<Matchup>>(
                default!,
                ResultStatus.Validation,
                validation.Errors);
        }

        var sql = _sqlProvider.GetMatchupsForSeasonWeek();

        var connection = _dbContext.Database.GetDbConnection();
        var result = await connection.QueryAsync<Matchup>(
            new CommandDefinition(sql, new { query.SeasonYear, query.SeasonWeekNumber, query.SeasonPhaseTypeCode }, cancellationToken: cancellationToken));

        return new Success<List<Matchup>>(result.ToList());
    }
}
