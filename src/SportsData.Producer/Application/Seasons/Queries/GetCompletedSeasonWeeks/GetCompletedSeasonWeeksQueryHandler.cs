using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Seasons.Queries.GetCompletedSeasonWeeks;

public interface IGetCompletedSeasonWeeksQueryHandler
{
    Task<Result<List<CanonicalSeasonWeekDto>>> ExecuteAsync(
        GetCompletedSeasonWeeksQuery query,
        CancellationToken cancellationToken = default);
}

public class GetCompletedSeasonWeeksQueryHandler : IGetCompletedSeasonWeeksQueryHandler
{
    private readonly TeamSportDataContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GetCompletedSeasonWeeksQueryHandler(
        TeamSportDataContext dbContext,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<List<CanonicalSeasonWeekDto>>> ExecuteAsync(
        GetCompletedSeasonWeeksQuery query,
        CancellationToken cancellationToken = default)
    {
        var now = _dateTimeProvider.UtcNow();

        var result = await _dbContext.SeasonWeeks
            .AsNoTracking()
            .Include(sw => sw.Season)
            .Include(sw => sw.SeasonPhase)
            .Where(sw => sw.Season!.Year == query.SeasonYear && sw.EndDate < now)
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
            .ToListAsync(cancellationToken);

        return new Success<List<CanonicalSeasonWeekDto>>(result);
    }
}
