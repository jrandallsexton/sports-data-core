using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Seasons.Queries.GetSeasonWeeksByDateRange;

public interface IGetSeasonWeeksByDateRangeQueryHandler
{
    Task<Result<List<CanonicalSeasonWeekDto>>> ExecuteAsync(
        GetSeasonWeeksByDateRangeQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Returns every <see cref="CanonicalSeasonWeekDto"/> whose
/// <c>[StartDate, EndDate]</c> overlaps the requested <c>[From, To]</c>
/// range, ordered by <c>StartDate</c> ascending.
/// </summary>
/// <remarks>
/// Overlap predicate: <c>StartDate &lt;= To AND EndDate &gt;= From</c>.
/// Inclusive on both bounds — a SeasonWeek that starts exactly on
/// <c>To</c> or ends exactly on <c>From</c> still overlaps.
///
/// <para>
/// Empty result is a legitimate <see cref="Success{T}"/> (not a
/// failure). The most common reason for an empty list is "no
/// SeasonWeeks are sourced yet for the requested date range" — e.g.
/// a 2027 league created in 2026 before next year's season calendar
/// has landed in the canonical DB. The API-side caller treats that
/// as "defer; the daily MatchupScheduler will pick it up once the
/// SeasonWeeks exist."
/// </para>
/// </remarks>
public class GetSeasonWeeksByDateRangeQueryHandler : IGetSeasonWeeksByDateRangeQueryHandler
{
    private readonly TeamSportDataContext _dbContext;

    public GetSeasonWeeksByDateRangeQueryHandler(TeamSportDataContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<List<CanonicalSeasonWeekDto>>> ExecuteAsync(
        GetSeasonWeeksByDateRangeQuery query,
        CancellationToken cancellationToken = default)
    {
        // Reject malformed input outright — a flipped range (From > To)
        // would silently return nothing and look the same as the legitimate
        // empty case. Better to fail fast at the API boundary.
        if (query.From > query.To)
        {
            return new Failure<List<CanonicalSeasonWeekDto>>(
                [],
                ResultStatus.Validation,
                [new FluentValidation.Results.ValidationFailure(
                    nameof(query.From),
                    "From must be on or before To.")]);
        }

        var weeks = await _dbContext.SeasonWeeks
            .AsNoTracking()
            .Include(sw => sw.Season)
            .Include(sw => sw.SeasonPhase)
            .Where(sw => sw.StartDate <= query.To && sw.EndDate >= query.From)
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

        return new Success<List<CanonicalSeasonWeekDto>>(weeks);
    }
}
