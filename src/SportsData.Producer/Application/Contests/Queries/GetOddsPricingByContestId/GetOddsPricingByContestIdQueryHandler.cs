using Dapper;

using FluentValidation;
using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Sql;

namespace SportsData.Producer.Application.Contests.Queries.GetOddsPricingByContestId;

public interface IGetOddsPricingByContestIdQueryHandler
{
    Task<Result<OddsPricingDto>> ExecuteAsync(
        GetOddsPricingByContestIdQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Current odds pricing for one contest (both teams' moneyline and spread
/// price, plus the over/under prices), from the same provider row the league
/// matchup queries use. Serves the API, which stores these on
/// PickemGroupMatchup and cannot reach this database. NotFound when the
/// contest does not exist here; a contest without odds returns null prices.
/// </summary>
public class GetOddsPricingByContestIdQueryHandler : IGetOddsPricingByContestIdQueryHandler
{
    private readonly TeamSportDataContext _dbContext;
    private readonly ProducerSqlQueryProvider _sqlProvider;
    private readonly IValidator<GetOddsPricingByContestIdQuery> _validator;

    public GetOddsPricingByContestIdQueryHandler(
        TeamSportDataContext dbContext,
        ProducerSqlQueryProvider sqlProvider,
        IValidator<GetOddsPricingByContestIdQuery> validator)
    {
        _dbContext = dbContext;
        _sqlProvider = sqlProvider;
        _validator = validator;
    }

    public async Task<Result<OddsPricingDto>> ExecuteAsync(
        GetOddsPricingByContestIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return new Failure<OddsPricingDto>(default!, ResultStatus.Validation, validation.Errors);
        }

        var connection = _dbContext.Database.GetDbConnection();
        var pricing = await connection.QuerySingleOrDefaultAsync<OddsPricingDto>(
            new CommandDefinition(
                _sqlProvider.GetOddsPricingByContestId(),
                new { query.ContestId },
                cancellationToken: cancellationToken));

        if (pricing is null)
        {
            return new Failure<OddsPricingDto>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(query.ContestId), $"Contest {query.ContestId} not found.")]);
        }

        return new Success<OddsPricingDto>(pricing);
    }
}
