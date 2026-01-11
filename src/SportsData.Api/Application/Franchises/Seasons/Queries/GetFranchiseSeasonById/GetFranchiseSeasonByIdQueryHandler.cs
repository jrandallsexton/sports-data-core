using FluentValidation.Results;

using SportsData.Api.Application.Franchises.Seasons;
using SportsData.Api.Infrastructure.Refs;
using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Franchise;
using SportsData.Core.Infrastructure.Clients.FranchiseSeason;

namespace SportsData.Api.Application.Franchises.Seasons.Queries.GetFranchiseSeasonById;

public interface IGetFranchiseSeasonByIdQueryHandler
{
    Task<Result<FranchiseSeasonResponseDto>> ExecuteAsync(GetFranchiseSeasonByIdQuery query, CancellationToken cancellationToken = default);
}

public class GetFranchiseSeasonByIdQueryHandler : IGetFranchiseSeasonByIdQueryHandler
{
    private readonly IFranchiseClientFactory _franchiseClientFactory;
    private readonly IFranchiseSeasonClientFactory _franchiseSeasonClientFactory;
    private readonly IGenerateApiResourceRefs _refGenerator;

    public GetFranchiseSeasonByIdQueryHandler(
        IFranchiseClientFactory franchiseClientFactory,
        IFranchiseSeasonClientFactory franchiseSeasonClientFactory,
        IGenerateApiResourceRefs refGenerator)
    {
        _franchiseClientFactory = franchiseClientFactory;
        _franchiseSeasonClientFactory = franchiseSeasonClientFactory;
        _refGenerator = refGenerator;
    }

    public async Task<Result<FranchiseSeasonResponseDto>> ExecuteAsync(
        GetFranchiseSeasonByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        // Step 1: Resolve franchise slug to GUID
        var franchiseClient = _franchiseClientFactory.Resolve(query.Sport, query.League);
        var franchiseResult = await franchiseClient.GetFranchiseById(query.FranchiseSlugOrId);

        if (franchiseResult is Failure<Core.Infrastructure.Clients.Franchise.Queries.GetFranchiseByIdResponse>)
        {
            return new Failure<FranchiseSeasonResponseDto>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure("FranchiseSlugOrId", $"Franchise '{query.FranchiseSlugOrId}' not found")]);
        }

        var franchise = ((Success<Core.Infrastructure.Clients.Franchise.Queries.GetFranchiseByIdResponse>)franchiseResult).Value.Franchise!;

        // Step 2: Get season from Producer
        var seasonClient = _franchiseSeasonClientFactory.Resolve(query.Sport, query.League);
        var seasonResult = await seasonClient.GetFranchiseSeasonById(franchise.Id, query.SeasonYear);

        if (seasonResult is Failure<Core.Infrastructure.Clients.FranchiseSeason.Queries.GetFranchiseSeasonByIdResponse>)
        {
            return new Failure<FranchiseSeasonResponseDto>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure("SeasonYear", $"Season {query.SeasonYear} not found for franchise '{franchise.Slug}'")]);
        }

        var season = ((Success<Core.Infrastructure.Clients.FranchiseSeason.Queries.GetFranchiseSeasonByIdResponse>)seasonResult).Value.Season!;

        // Step 3: Enrich with HATEOAS
        var response = new FranchiseSeasonResponseDto
        {
            Id = season.Id,
            FranchiseId = season.FranchiseId,
            SeasonYear = season.SeasonYear,
            Slug = season.Slug,
            Location = season.Location,
            Name = season.Name,
            Abbreviation = season.Abbreviation,
            DisplayName = season.DisplayName,
            DisplayNameShort = season.DisplayNameShort,
            ColorCodeHex = season.ColorCodeHex,
            ColorCodeAltHex = season.ColorCodeAltHex,
            IsActive = season.IsActive,
            Wins = season.Wins,
            Losses = season.Losses,
            Ties = season.Ties,
            ConferenceWins = season.ConferenceWins,
            ConferenceLosses = season.ConferenceLosses,
            ConferenceTies = season.ConferenceTies,
            Ref = _refGenerator.ForFranchiseSeason(franchise.Id, season.SeasonYear, query.Sport, query.League),
            Links = new Dictionary<string, Uri>
            {
                ["self"] = _refGenerator.ForFranchiseSeason(franchise.Id, season.SeasonYear, query.Sport, query.League),
                ["franchise"] = _refGenerator.ForFranchise(franchise.Id, query.Sport, query.League),
                ["contests"] = _refGenerator.ForSeasonContests(franchise.Id, season.SeasonYear, query.Sport, query.League)
            }
        };

        return new Success<FranchiseSeasonResponseDto>(response);
    }
}
