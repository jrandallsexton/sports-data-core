using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Seasons.Queries.GetSeasonOverview;

public interface IGetSeasonOverviewQueryHandler
{
    Task<Result<SeasonOverviewDto>> ExecuteAsync(GetSeasonOverviewQuery query, CancellationToken cancellationToken = default);
}

public class GetSeasonOverviewQueryHandler : IGetSeasonOverviewQueryHandler
{
    private readonly TeamSportDataContext _dbContext;

    public GetSeasonOverviewQueryHandler(TeamSportDataContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<SeasonOverviewDto>> ExecuteAsync(
        GetSeasonOverviewQuery query,
        CancellationToken cancellationToken = default)
    {
        var season = await _dbContext.Seasons
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Year == query.SeasonYear, cancellationToken);

        if (season is null)
        {
            return new Failure<SeasonOverviewDto>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure("SeasonYear", $"Season with year {query.SeasonYear} not found")]);
        }

        var weeks = await _dbContext.SeasonWeeks
            .AsNoTracking()
            .Include(w => w.SeasonPhase)
            .Where(w => w.SeasonId == season.Id)
            .OrderBy(w => w.StartDate)
            .Select(w => new SeasonWeekDto
            {
                Id = w.Id,
                Number = w.Number,
                Label = w.SeasonPhase.Name == "Regular Season"
                    ? $"Week {w.Number}"
                    : $"{w.SeasonPhase.Name} - Week {w.Number}",
                SeasonPhaseName = w.SeasonPhase.Name,
                StartDate = w.StartDate,
                EndDate = w.EndDate
            })
            .ToListAsync(cancellationToken);

        var polls = await _dbContext.SeasonPolls
            .AsNoTracking()
            .Where(p => p.SeasonYear == query.SeasonYear)
            .Select(p => new SeasonPollSummaryDto
            {
                Id = p.Id,
                Name = p.Name,
                ShortName = p.ShortName,
                Slug = p.Slug
            })
            .ToListAsync(cancellationToken);

        var dto = new SeasonOverviewDto
        {
            SeasonYear = season.Year,
            Name = season.Name,
            StartDate = season.StartDate,
            EndDate = season.EndDate,
            Weeks = weeks,
            Polls = polls
        };

        return new Success<SeasonOverviewDto>(dto);
    }
}
