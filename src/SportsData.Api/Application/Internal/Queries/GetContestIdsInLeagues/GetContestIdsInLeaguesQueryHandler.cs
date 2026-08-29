using FluentValidation;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

namespace SportsData.Api.Application.Internal.Queries.GetContestIdsInLeagues;

public record GetContestIdsInLeaguesQuery(Guid[] ContestIds);

public class GetContestIdsInLeaguesQueryValidator : AbstractValidator<GetContestIdsInLeaguesQuery>
{
    public GetContestIdsInLeaguesQueryValidator()
    {
        // Empty is VALID (answered [] without a query); null and oversized
        // are not — the 5,000 bound keeps a malformed caller from turning
        // the probe into a giant ANY() scan. Producer batches per season
        // week (< 1k).
        RuleFor(x => x.ContestIds).NotNull();
        RuleFor(x => x.ContestIds)
            .Must(ids => ids.Length <= 5000)
            .When(x => x.ContestIds is not null)
            .WithMessage("Too many contest ids; batch the request.");
    }
}

public interface IGetContestIdsInLeaguesQueryHandler
{
    Task<Result<List<Guid>>> ExecuteAsync(
        GetContestIdsInLeaguesQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Which of the supplied contests appear in any league's matchups (team
/// or player pick'em — both generate PickemGroupMatchup rows), any season
/// week. Powers Producer's stream-scheduling filter so live-sourcing
/// covers only games that back a league.
/// </summary>
public class GetContestIdsInLeaguesQueryHandler : IGetContestIdsInLeaguesQueryHandler
{
    private readonly AppDataContext _dataContext;
    private readonly IValidator<GetContestIdsInLeaguesQuery> _validator;

    public GetContestIdsInLeaguesQueryHandler(
        AppDataContext dataContext,
        IValidator<GetContestIdsInLeaguesQuery> validator)
    {
        _dataContext = dataContext;
        _validator = validator;
    }

    public async Task<Result<List<Guid>>> ExecuteAsync(
        GetContestIdsInLeaguesQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return new Failure<List<Guid>>(default!, ResultStatus.BadRequest, validation.Errors);
        }

        if (query.ContestIds.Length == 0)
        {
            return new Success<List<Guid>>([]);
        }

        var inUse = await _dataContext.PickemGroupMatchups
            .AsNoTracking()
            .Where(m => query.ContestIds.Contains(m.ContestId))
            .Select(m => m.ContestId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return new Success<List<Guid>>(inUse);
    }
}
