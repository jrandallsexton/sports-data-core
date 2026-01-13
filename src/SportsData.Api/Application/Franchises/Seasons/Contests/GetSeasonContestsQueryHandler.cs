using FluentValidation.Results;
using SportsData.Api.Infrastructure.Refs;
using SportsData.Core.Common;
using SportsData.Core.Common.Mapping;
using SportsData.Core.Infrastructure.Clients.Contest;
using SportsData.Core.Infrastructure.Clients.Franchise;

namespace SportsData.Api.Application.Franchises.Seasons.Contests;

public interface IGetSeasonContestsQueryHandler
{
    Task<Result<GetSeasonContestsResponseDto>> ExecuteAsync(GetSeasonContestsQuery query, CancellationToken cancellationToken);
}

public class GetSeasonContestsQueryHandler : IGetSeasonContestsQueryHandler
{
    private readonly IFranchiseClientFactory _franchiseClientFactory;
    private readonly IContestClientFactory _contestClientFactory;
    private readonly IGenerateApiResourceRefs _refGenerator;

    public GetSeasonContestsQueryHandler(
        IFranchiseClientFactory franchiseClientFactory,
        IContestClientFactory contestClientFactory,
        IGenerateApiResourceRefs refGenerator)
    {
        _franchiseClientFactory = franchiseClientFactory;
        _contestClientFactory = contestClientFactory;
        _refGenerator = refGenerator;
    }

    public async Task<Result<GetSeasonContestsResponseDto>> ExecuteAsync(GetSeasonContestsQuery query, CancellationToken cancellationToken)
    {
        // Resolve sport/league to mode
        Sport mode;
        try
        {
            mode = ModeMapper.ResolveMode(query.Sport, query.League);
        }
        catch (NotSupportedException ex)
        {
            return new Failure<GetSeasonContestsResponseDto>(
                default!,
                ResultStatus.BadRequest,
                [new ValidationFailure("Sport/League", ex.Message)]);
        }

        // Step 1: Resolve franchise slug to GUID
        var franchiseClient = _franchiseClientFactory.Resolve(mode);
        var franchiseResult = await franchiseClient.GetFranchiseById(query.FranchiseId);

        if (franchiseResult is Failure<Core.Infrastructure.Clients.Franchise.Queries.GetFranchiseByIdResponse>)
        {
            return new Failure<GetSeasonContestsResponseDto>(
                default!,
                ResultStatus.NotFound,
                [new FluentValidation.Results.ValidationFailure("FranchiseSlugOrId", $"Franchise '{query.FranchiseId}' not found")]);
        }

        var franchise = ((Success<Core.Infrastructure.Clients.Franchise.Queries.GetFranchiseByIdResponse>)franchiseResult).Value.Franchise!;

        // Step 2: Get contests from Producer
        var contestClient = _contestClientFactory.Resolve(mode);
        var contestsResult = await contestClient.GetSeasonContests(
            franchise.Id,
            query.SeasonYear,
            query.Week,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        if (contestsResult is Failure<Core.Infrastructure.Clients.Contest.Queries.GetSeasonContestsResponse>)
        {
            return new Failure<GetSeasonContestsResponseDto>(
                default!,
                ResultStatus.NotFound,
                [new FluentValidation.Results.ValidationFailure("Contests", "Failed to retrieve contests")]);
        }

        var contestsData = ((Success<Core.Infrastructure.Clients.Contest.Queries.GetSeasonContestsResponse>)contestsResult).Value;

        // Step 3: Enrich with HATEOAS using GUIDs
        var enrichedContests = new List<ContestResponseDto>(contestsData.Contests.Count);
        
        foreach (var contest in contestsData.Contests)
        {
            var contestRef = _refGenerator.ForContest(contest.Id, query.Sport, query.League).ToString();
            
            enrichedContests.Add(new ContestResponseDto
            {
                Id = contest.Id,
                Slug = contest.Slug,
                Name = contest.Name,
                ShortName = contest.ShortName,
                StartDateUtc = contest.StartDateUtc,
                Sport = contest.Sport,
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
                Ref = contestRef,
                Links = new ContestLinks
                {
                    Self = contestRef,
                    HomeTeam = string.Empty, // TODO: Need FranchiseId in SeasonContestDto to generate team links
                    AwayTeam = string.Empty, // TODO: Need FranchiseId in SeasonContestDto to generate team links
                    Venue = contest.VenueId.HasValue 
                        ? _refGenerator.ForVenue(contest.VenueId.Value, query.Sport, query.League).ToString() 
                        : null
                }
            });
        }

        var response = new GetSeasonContestsResponseDto
        {
            Items = enrichedContests,
            TotalCount = enrichedContests.Count,
            Filters = new SeasonContestsFilters
            {
                SeasonYear = query.SeasonYear,
                FranchiseSlug = franchise!.Slug,
                Week = query.Week
            },
            Links = new SeasonContestsLinks
            {
                Self = _refGenerator.ForSeasonContests(franchise.Id, query.SeasonYear, query.Sport, query.League, query.Week).ToString(),
                Franchise = _refGenerator.ForFranchise(franchise.Id, query.Sport, query.League).ToString(),
                Season = _refGenerator.ForFranchiseSeason(franchise.Id, query.SeasonYear, query.Sport, query.League).ToString()
            }
        };

        return new Success<GetSeasonContestsResponseDto>(response);
    }
}
