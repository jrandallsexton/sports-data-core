using Microsoft.EntityFrameworkCore;
using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Contest.Queries;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Franchises.Queries.GetSeasonContests;

public interface IGetSeasonContestsQueryHandler
{
    Task<Result<List<SeasonContestDto>>> Handle(GetSeasonContestsQuery query, CancellationToken cancellationToken);
}

public class GetSeasonContestsQueryHandler : IGetSeasonContestsQueryHandler
{
    private readonly FootballDataContext _context;

    public GetSeasonContestsQueryHandler(FootballDataContext context)
    {
        _context = context;
    }

    public async Task<Result<List<SeasonContestDto>>> Handle(GetSeasonContestsQuery query, CancellationToken cancellationToken)
    {
        var contestsQuery = _context.Contests
            .AsNoTracking()
            .Where(c => c.SeasonYear == query.SeasonYear &&
                       (c.HomeTeamFranchiseSeason.FranchiseId == query.FranchiseId ||
                        c.AwayTeamFranchiseSeason.FranchiseId == query.FranchiseId));

        // Optional week filter
        if (query.Week.HasValue)
        {
            contestsQuery = contestsQuery.Where(c => c.Week == query.Week.Value);
        }

        var contests = await contestsQuery
            .OrderBy(c => c.StartDateUtc)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(c => new SeasonContestDto
            {
                Id = c.Id,
                Slug = $"{c.AwayTeamFranchiseSeason.Slug}-vs-{c.HomeTeamFranchiseSeason.Slug}-{c.StartDateUtc:yyyy-MM-dd}",
                Name = c.Name,
                ShortName = c.ShortName,
                StartDateUtc = c.StartDateUtc,
                Sport = c.Sport,
                SeasonYear = c.SeasonYear,
                Week = c.Week,
                HomeTeamFranchiseSeasonId = c.HomeTeamFranchiseSeasonId,
                HomeTeamSlug = c.HomeTeamFranchiseSeason.Slug,
                HomeTeamDisplayName = c.HomeTeamFranchiseSeason.DisplayName,
                HomeScore = c.HomeScore,
                AwayTeamFranchiseSeasonId = c.AwayTeamFranchiseSeasonId,
                AwayTeamSlug = c.AwayTeamFranchiseSeason.Slug,
                AwayTeamDisplayName = c.AwayTeamFranchiseSeason.DisplayName,
                AwayScore = c.AwayScore,
                VenueId = c.VenueId,
                FinalizedUtc = c.FinalizedUtc,
                IsFinal = c.FinalizedUtc.HasValue,
                CreatedUtc = c.CreatedUtc
            })
            .ToListAsync(cancellationToken);

        return new Success<List<SeasonContestDto>>(contests);
    }
}
