using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Seasons.Queries.GetCurrentSeasonWeek;

public interface IGetCurrentSeasonWeekQueryHandler
{
    Task<Result<CanonicalSeasonWeekDto>> ExecuteAsync(
        GetCurrentSeasonWeekQuery query,
        CancellationToken cancellationToken = default);
}

public class GetCurrentSeasonWeekQueryHandler : IGetCurrentSeasonWeekQueryHandler
{
    private readonly TeamSportDataContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GetCurrentSeasonWeekQueryHandler(
        TeamSportDataContext dbContext,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<CanonicalSeasonWeekDto>> ExecuteAsync(
        GetCurrentSeasonWeekQuery query,
        CancellationToken cancellationToken = default)
    {
        var now = _dateTimeProvider.UtcNow();

        var result = await _dbContext.SeasonWeeks
            .AsNoTracking()
            .Include(sw => sw.Season)
            .Include(sw => sw.SeasonPhase)
            .Where(sw => sw.StartDate <= now && sw.EndDate > now)
            .OrderBy(sw => sw.StartDate)
            .Select(sw => new CanonicalSeasonWeekDto
            {
                Id = sw.Id,
                SeasonId = sw.SeasonId,
                SeasonYear = sw.Season!.Year,
                WeekNumber = sw.Number,
                SeasonPhase = sw.SeasonPhase!.Name,
                IsNonStandardWeek = sw.IsNonStandardWeek
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (result is null)
        {
            return new Failure<CanonicalSeasonWeekDto>(
                default!,
                ResultStatus.NotFound,
                [new FluentValidation.Results.ValidationFailure("SeasonWeek", "No current season week found")]);
        }

        return new Success<CanonicalSeasonWeekDto>(result);
    }
}
