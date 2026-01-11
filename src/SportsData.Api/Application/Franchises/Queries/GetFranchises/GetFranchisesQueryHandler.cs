using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Franchise.Queries;
using SportsData.Api.Infrastructure.Refs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SportsData.Api.Application.Franchises.Queries.GetFranchises;

public interface IGetFranchisesQueryHandler
{
    Task<Result<GetFranchisesResponseDto>> ExecuteAsync(GetFranchisesQuery query, CancellationToken cancellationToken = default);
}

public class GetFranchisesQueryHandler : IGetFranchisesQueryHandler
{
    private readonly IFranchiseClientFactory _franchiseClientFactory;
    private readonly IGenerateApiResourceRefs _refGenerator;
    private readonly ILogger<GetFranchisesQueryHandler> _logger;

    public GetFranchisesQueryHandler(
        IFranchiseClientFactory franchiseClientFactory,
        IGenerateApiResourceRefs refGenerator,
        ILogger<GetFranchisesQueryHandler> logger)
    {
        _franchiseClientFactory = franchiseClientFactory;
        _refGenerator = refGenerator;
        _logger = logger;
    }

    public async Task<Result<GetFranchisesResponseDto>> ExecuteAsync(
        GetFranchisesQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("GetFranchises query: sport={Sport}, league={League}, page={PageNumber}, size={PageSize}",
            query.Sport, query.League, query.PageNumber, query.PageSize);

        // Resolve the appropriate client for this sport/league
        var client = _franchiseClientFactory.Resolve(query.Sport, query.League);

        // Get canonical response from Producer
        var franchisesResult = await client.GetFranchises(query.PageNumber, query.PageSize);

        if (franchisesResult is Failure<GetFranchisesResponse> failure)
        {
            _logger.LogWarning(
                "GetFranchises failed. Sport={Sport}, League={League}, Status={Status}",
                query.Sport,
                query.League,
                failure.Status);
            
            return new Failure<GetFranchisesResponseDto>(
                new GetFranchisesResponseDto(),
                failure.Status,
                failure.Errors);
        }

        var canonicalResponse = (franchisesResult as Success<GetFranchisesResponse>)!.Value;

        // Enrich with HATEOAS
        var enrichedResponse = new GetFranchisesResponseDto
        {
            Items = canonicalResponse.Items.Select(f => EnrichFranchise(f, query.Sport, query.League)).ToList(),

            // Copy pagination metadata
            TotalCount = canonicalResponse.TotalCount,
            PageNumber = canonicalResponse.PageNumber,
            PageSize = canonicalResponse.PageSize,
            TotalPages = canonicalResponse.TotalPages,
            HasPreviousPage = canonicalResponse.HasPreviousPage,
            HasNextPage = canonicalResponse.HasNextPage,

            // Add HATEOAS links
            Links = new Dictionary<string, Uri>
            {
                ["self"] = _refGenerator.ForFranchises(query.Sport, query.League, canonicalResponse.PageNumber, canonicalResponse.PageSize),
                ["first"] = _refGenerator.ForFranchises(query.Sport, query.League, 1, canonicalResponse.PageSize),
                ["last"] = _refGenerator.ForFranchises(query.Sport, query.League,
                    canonicalResponse.TotalPages > 0 ? canonicalResponse.TotalPages : 1,
                    canonicalResponse.PageSize)
            }
        };

        // Add prev link if not on first page
        if (canonicalResponse.HasPreviousPage)
        {
            enrichedResponse.Links["prev"] = _refGenerator.ForFranchises(
                query.Sport, query.League,
                canonicalResponse.PageNumber - 1,
                canonicalResponse.PageSize);
        }

        // Add next link if not on last page
        if (canonicalResponse.HasNextPage)
        {
            enrichedResponse.Links["next"] = _refGenerator.ForFranchises(
                query.Sport, query.League,
                canonicalResponse.PageNumber + 1,
                canonicalResponse.PageSize);
        }

        return new Success<GetFranchisesResponseDto>(enrichedResponse);
    }

    /// <summary>
    /// Transforms canonical FranchiseDto into enriched FranchiseResponseDto with HATEOAS refs.
    /// </summary>
    private FranchiseResponseDto EnrichFranchise(SportsData.Core.Dtos.Canonical.FranchiseDto canonical, string sport, string league)
    {
        return new FranchiseResponseDto
        {
            Id = canonical.Id,
            Sport = (int)canonical.Sport,
            Name = canonical.Name,
            Nickname = canonical.Nickname,
            Abbreviation = canonical.Abbreviation,
            DisplayName = canonical.DisplayName,
            DisplayNameShort = canonical.DisplayNameShort,
            ColorCodeHex = canonical.ColorCodeHex,
            ColorCodeAltHex = canonical.ColorCodeAltHex,
            Slug = canonical.Slug,

            // Add HATEOAS refs using GUID
            Ref = _refGenerator.ForFranchise(canonical.Id, sport, league),
            Links = new Dictionary<string, Uri>
            {
                ["self"] = _refGenerator.ForFranchise(canonical.Id, sport, league),
                ["seasons"] = _refGenerator.ForFranchiseSeasons(canonical.Id, sport, league)
            }
        };
    }
}
