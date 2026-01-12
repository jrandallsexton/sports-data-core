using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Franchise;
using SportsData.Api.Infrastructure.Refs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;

namespace SportsData.Api.Application.Franchises.Seasons.Queries.GetFranchiseSeasons;

public interface IGetFranchiseSeasonsQueryHandler
{
    Task<Result<GetFranchiseSeasonsResponseDto>> ExecuteAsync(GetFranchiseSeasonsQuery query, CancellationToken cancellationToken = default);
}

public class GetFranchiseSeasonsQueryHandler : IGetFranchiseSeasonsQueryHandler
{
    private readonly IFranchiseClientFactory _franchiseClientFactory;
    private readonly IGenerateApiResourceRefs _refGenerator;
    private readonly ILogger<GetFranchiseSeasonsQueryHandler> _logger;

    public GetFranchiseSeasonsQueryHandler(
        IFranchiseClientFactory franchiseClientFactory,
        IGenerateApiResourceRefs refGenerator,
        ILogger<GetFranchiseSeasonsQueryHandler> logger)
    {
        _franchiseClientFactory = franchiseClientFactory;
        _refGenerator = refGenerator;
        _logger = logger;
    }

    public async Task<Result<GetFranchiseSeasonsResponseDto>> ExecuteAsync(
        GetFranchiseSeasonsQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("GetFranchiseSeasons query: sport={Sport}, league={League}, franchiseIdOrSlug={FranchiseIdOrSlug}",
            query.Sport, query.League, query.FranchiseIdOrSlug);

        // First, resolve franchise slug to ID if needed
        var franchiseClient = _franchiseClientFactory.Resolve(query.Sport, query.League);
        var franchiseResult = await franchiseClient.GetFranchiseById(query.FranchiseIdOrSlug);

        if (franchiseResult is Failure<Core.Infrastructure.Clients.Franchise.Queries.GetFranchiseByIdResponse> franchiseFailure)
        {
            _logger.LogWarning(
                "Franchise not found. Sport={Sport}, League={League}, IdOrSlug={IdOrSlug}, Status={Status}",
                query.Sport,
                query.League,
                query.FranchiseIdOrSlug,
                franchiseFailure.Status);

            return new Failure<GetFranchiseSeasonsResponseDto>(
                default!,
                franchiseFailure.Status,
                franchiseFailure.Errors);
        }

        var franchise = (franchiseResult as Success<Core.Infrastructure.Clients.Franchise.Queries.GetFranchiseByIdResponse>)!.Value.Franchise!;

        // Now get seasons for this franchise using the GUID
        var seasonsResult = await franchiseClient.GetFranchiseSeasons(franchise.Id);

        if (seasonsResult is Failure<Core.Infrastructure.Clients.Franchise.Queries.GetFranchiseSeasonsResponse> seasonsFailure)
        {
            _logger.LogWarning(
                "GetFranchiseSeasons failed. Sport={Sport}, League={League}, FranchiseId={FranchiseId}, Status={Status}",
                query.Sport,
                query.League,
                franchise.Id,
                seasonsFailure.Status);

            return new Failure<GetFranchiseSeasonsResponseDto>(
                default!,
                seasonsFailure.Status,
                seasonsFailure.Errors);
        }

        var canonicalSeasons = (seasonsResult as Success<Core.Infrastructure.Clients.Franchise.Queries.GetFranchiseSeasonsResponse>)!.Value.Seasons;

        // Enrich with HATEOAS
        var enrichedResponse = new GetFranchiseSeasonsResponseDto
        {
            FranchiseId = franchise.Id,
            FranchiseSlug = franchise.Slug,
            Items = canonicalSeasons.Select(s => EnrichFranchiseSeason(s, query.Sport, query.League, franchise.Id)).ToList(),
            Links = new Dictionary<string, Uri>
            {
                ["self"] = _refGenerator.ForFranchiseSeasons(franchise.Id, query.Sport, query.League),
                ["franchise"] = _refGenerator.ForFranchise(franchise.Id, query.Sport, query.League)
            }
        };

        return new Success<GetFranchiseSeasonsResponseDto>(enrichedResponse);
    }

    private FranchiseSeasonResponseDto EnrichFranchiseSeason(
        Core.Dtos.Canonical.FranchiseSeasonDto canonical,
        string sport,
        string league,
        Guid franchiseId)
    {
        return new FranchiseSeasonResponseDto
        {
            Id = canonical.Id,
            FranchiseId = canonical.FranchiseId,
            SeasonYear = canonical.SeasonYear,
            Slug = canonical.Slug,
            Location = canonical.Location,
            Name = canonical.Name,
            Abbreviation = canonical.Abbreviation,
            DisplayName = canonical.DisplayName,
            DisplayNameShort = canonical.DisplayNameShort,
            ColorCodeHex = canonical.ColorCodeHex,
            ColorCodeAltHex = canonical.ColorCodeAltHex,
            IsActive = canonical.IsActive,
            Wins = canonical.Wins,
            Losses = canonical.Losses,
            Ties = canonical.Ties,
            ConferenceWins = canonical.ConferenceWins,
            ConferenceLosses = canonical.ConferenceLosses,
            ConferenceTies = canonical.ConferenceTies,

            // HATEOAS using franchiseId and year
            Ref = _refGenerator.ForFranchiseSeason(franchiseId, canonical.SeasonYear, sport, league),
            Links = new Dictionary<string, Uri>
            {
                ["self"] = _refGenerator.ForFranchiseSeason(franchiseId, canonical.SeasonYear, sport, league),
                ["franchise"] = _refGenerator.ForFranchise(franchiseId, sport, league),
                ["contests"] = _refGenerator.ForSeasonContests(franchiseId, canonical.SeasonYear, sport, league)
            }
        };
    }
}
