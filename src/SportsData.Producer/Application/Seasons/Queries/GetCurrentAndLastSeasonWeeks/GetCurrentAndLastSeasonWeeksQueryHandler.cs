using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Seasons.Queries.GetCurrentAndLastSeasonWeeks;

public interface IGetCurrentAndLastSeasonWeeksQueryHandler
{
    Task<Result<List<CanonicalSeasonWeekDto>>> ExecuteAsync(
        GetCurrentAndLastSeasonWeeksQuery query,
        CancellationToken cancellationToken = default);
}

public class GetCurrentAndLastSeasonWeeksQueryHandler : IGetCurrentAndLastSeasonWeeksQueryHandler
{
    private readonly TeamSportDataContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GetCurrentAndLastSeasonWeeksQueryHandler(
        TeamSportDataContext dbContext,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<List<CanonicalSeasonWeekDto>>> ExecuteAsync(
        GetCurrentAndLastSeasonWeeksQuery query,
        CancellationToken cancellationToken = default)
    {
        var now = _dateTimeProvider.UtcNow();
        // 13 days covers two football weeks (7 days each) plus buffer for scheduling shifts
        var thirteenDaysAgo = now.AddDays(-13);

        var result = await _dbContext.SeasonWeeks
            .AsNoTracking()
            .Include(sw => sw.Season)
            .Include(sw => sw.SeasonPhase)
            .Where(sw =>
                (sw.StartDate <= now && sw.EndDate > now) ||
                (sw.StartDate >= thirteenDaysAgo && sw.StartDate < now))
            .OrderByDescending(sw => sw.StartDate)
            .Take(2)
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
