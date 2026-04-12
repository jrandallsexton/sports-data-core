using Dapper;

using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Sql;

using System.Collections.Generic;
using System.Linq;

namespace SportsData.Producer.Application.Franchises.Queries.GetTeamCard;

public interface IGetTeamCardQueryHandler
{
    Task<Result<TeamCardDto>> ExecuteAsync(
        GetTeamCardQuery query,
        CancellationToken cancellationToken = default);
}

public class GetTeamCardQueryHandler : IGetTeamCardQueryHandler
{
    private readonly TeamSportDataContext _dbContext;
    private readonly ProducerSqlQueryProvider _sqlProvider;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GetTeamCardQueryHandler(
        TeamSportDataContext dbContext,
        ProducerSqlQueryProvider sqlProvider,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _sqlProvider = sqlProvider;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<TeamCardDto>> ExecuteAsync(
        GetTeamCardQuery query,
        CancellationToken cancellationToken = default)
    {
        var connection = _dbContext.Database.GetDbConnection();
        var parameters = new { query.Slug, query.SeasonYear, NowUtc = _dateTimeProvider.UtcNow() };

        var teamCard = await connection.QueryFirstOrDefaultAsync<TeamCardDto>(
            new CommandDefinition(_sqlProvider.GetTeamCard(), parameters, cancellationToken: cancellationToken));

        if (teamCard is null)
        {
            return new Failure<TeamCardDto>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure("TeamCard", $"Team card not found for slug '{query.Slug}' season {query.SeasonYear}")]);
        }

        var seasons = (await connection.QueryAsync<int>(
            new CommandDefinition(_sqlProvider.GetTeamSeasons(), new { query.Slug }, cancellationToken: cancellationToken))).ToList();

        var schedule = (await connection.QueryAsync<TeamCardScheduleItemDto>(
            new CommandDefinition(_sqlProvider.GetTeamCardSchedule(), parameters, cancellationToken: cancellationToken))).ToList();

        var result = teamCard with
        {
            SeasonYears = seasons,
            Schedule = schedule
        };

        return new Success<TeamCardDto>(result);
    }
}
