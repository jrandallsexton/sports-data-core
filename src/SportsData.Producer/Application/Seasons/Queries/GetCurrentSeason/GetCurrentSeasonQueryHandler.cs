using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Seasons.Queries.GetCurrentSeason;

public interface IGetCurrentSeasonQueryHandler
{
    Task<Result<CurrentSeasonDto>> ExecuteAsync(
        GetCurrentSeasonQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolves the current-or-upcoming season with its phases. See
/// <see cref="GetCurrentSeasonQuery"/> for the "current" definition.
/// </summary>
public class GetCurrentSeasonQueryHandler : IGetCurrentSeasonQueryHandler
{
    private readonly TeamSportDataContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GetCurrentSeasonQueryHandler(
        TeamSportDataContext dbContext,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<CurrentSeasonDto>> ExecuteAsync(
        GetCurrentSeasonQuery query,
        CancellationToken cancellationToken = default)
    {
        var now = _dateTimeProvider.UtcNow();

        // Current-or-upcoming: earliest season not yet ended. In-season this is
        // the in-progress season (its EndDate is still future, which also covers
        // the Jan–Feb playoff window); off-season the prior season's EndDate has
        // passed so the next upcoming season wins.
        var season = await _dbContext.Seasons
            .AsNoTracking()
            .Where(s => s.EndDate >= now)
            .OrderBy(s => s.StartDate)
            .Select(s => new CurrentSeasonDto
            {
                SeasonYear = s.Year,
                Name = s.Name,
                StartDate = s.StartDate,
                EndDate = s.EndDate,
                Phases = s.Phases
                    .OrderBy(p => p.StartDate)
                    .Select(p => new SeasonPhaseDto
                    {
                        TypeCode = p.TypeCode,
                        Name = p.Name,
                        StartDate = p.StartDate,
                        EndDate = p.EndDate
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (season is null)
        {
            return new Failure<CurrentSeasonDto>(
                default!,
                ResultStatus.NotFound,
                [new FluentValidation.Results.ValidationFailure(
                    "Season", "No current or upcoming season found")]);
        }

        return new Success<CurrentSeasonDto>(season);
    }
}
