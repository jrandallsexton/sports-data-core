using SportsData.Api.Application.Contests.Queries.GetContestById.Dtos;
using SportsData.Api.Infrastructure.Refs;
using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Contest;

namespace SportsData.Api.Application.Contests.Queries.GetContestById;

public class GetContestByIdQueryHandler : IGetContestByIdQueryHandler
{
    private readonly IContestClientFactory _contestClientFactory;
    private readonly IGenerateApiResourceRefs _refGenerator;

    public GetContestByIdQueryHandler(
        IContestClientFactory contestClientFactory,
        IGenerateApiResourceRefs refGenerator)
    {
        _contestClientFactory = contestClientFactory;
        _refGenerator = refGenerator;
    }

    public async Task<Result<ContestDetailResponseDto>> ExecuteAsync(
        GetContestByIdQuery query,
        CancellationToken cancellationToken)
    {
        var contestClient = _contestClientFactory.Resolve(query.Sport, query.League);
        var contestResult = await contestClient.GetContestById(
            query.ContestId,
            cancellationToken);

        return contestResult switch
        {
            Failure<Core.Infrastructure.Clients.Contest.Queries.GetContestByIdResponse> failure when failure.Status == ResultStatus.NotFound =>
                new Failure<ContestDetailResponseDto>(
                    default!,
                    ResultStatus.NotFound,
                    [new FluentValidation.Results.ValidationFailure("ContestId", $"Contest '{query.ContestId}' not found")]),
            
            Failure<Core.Infrastructure.Clients.Contest.Queries.GetContestByIdResponse> failure =>
                new Failure<ContestDetailResponseDto>(
                    default!,
                    failure.Status,
                    failure.Errors),
            
            Success<Core.Infrastructure.Clients.Contest.Queries.GetContestByIdResponse> success =>
                new Success<ContestDetailResponseDto>(EnrichContest(success.Value.Contest, query.Sport, query.League)),
            
            _ => throw new InvalidOperationException("Unexpected result type")
        };
    }

    private ContestDetailResponseDto EnrichContest(
        Core.Infrastructure.Clients.Contest.Queries.SeasonContestDto contest,
        string sport,
        string league)
    {
        var contestRef = _refGenerator.ForContest(contest.Id, sport, league);
        var venueRef = contest.VenueId.HasValue
            ? _refGenerator.ForVenue(contest.VenueId.Value, sport, league).ToString()
            : string.Empty;

        // TODO: Need FranchiseId in SeasonContestDto to generate proper franchise links
        var homeTeamRef = string.Empty;
        var awayTeamRef = string.Empty;

        return new ContestDetailResponseDto
        {
            Id = contest.Id,
            Slug = contest.Slug,
            Name = contest.Name,
            ShortName = contest.ShortName,
            StartDateUtc = contest.StartDateUtc,
            Sport = contest.Sport.ToString(),
            SeasonYear = contest.SeasonYear,
            Week = contest.Week,
            
            HomeTeamFranchiseSeasonId = contest.HomeTeamFranchiseSeasonId,
            HomeTeamSlug = contest.HomeTeamSlug,
            HomeTeamDisplayName = contest.HomeTeamDisplayName,
            HomeScore = contest.HomeScore,
            
            AwayTeamFranchiseSeasonId = contest.AwayTeamFranchiseSeasonId,
            AwayTeamSlug = contest.AwayTeamSlug,
            AwayTeamDisplayName = contest.AwayTeamDisplayName,
            AwayScore = contest.AwayScore,
            
            IsFinal = contest.IsFinal,
            
            Ref = contestRef.ToString(),
            Links = new ContestDetailLinks
            {
                Self = contestRef.ToString(),
                HomeTeam = homeTeamRef,
                AwayTeam = awayTeamRef,
                Venue = venueRef
            }
        };
    }
}
