using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Contest.Queries;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Contests.Queries.GetContestById;

public interface IGetContestByIdQueryHandler
{
    Task<Result<SeasonContestDto>> ExecuteAsync(GetContestByIdQuery query, CancellationToken cancellationToken = default);
}

public class GetContestByIdQueryHandler : IGetContestByIdQueryHandler
{
    private readonly FootballDataContext _context;

    public GetContestByIdQueryHandler(FootballDataContext context)
    {
        _context = context;
    }

    public async Task<Result<SeasonContestDto>> ExecuteAsync(
        GetContestByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var contest = await _context.Contests
            .AsNoTracking()
            .Where(c => c.Id == query.ContestId)
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
            .FirstOrDefaultAsync(cancellationToken);

        if (contest == null)
        {
            return new Failure<SeasonContestDto>(
                default!,
                ResultStatus.NotFound,
                [new FluentValidation.Results.ValidationFailure("ContestId", $"Contest {query.ContestId} not found")]);
        }

        return new Success<SeasonContestDto>(contest);
    }
}
