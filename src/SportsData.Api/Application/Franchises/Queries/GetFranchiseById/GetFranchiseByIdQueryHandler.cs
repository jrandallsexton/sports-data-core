using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Franchise;
using SportsData.Core.Infrastructure.Clients.Franchise.Queries;
using IProvideFranchises = SportsData.Core.Infrastructure.Clients.Franchise.IProvideFranchises;
using SportsData.Api.Infrastructure.Refs;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;

namespace SportsData.Api.Application.Franchises.Queries.GetFranchiseById;

public interface IGetFranchiseByIdQueryHandler
{
    Task<Result<FranchiseResponseDto>> ExecuteAsync(GetFranchiseByIdQuery query, CancellationToken cancellationToken = default);
}

public class GetFranchiseByIdQueryHandler : IGetFranchiseByIdQueryHandler
{
    private readonly IFranchiseClientFactory _franchiseClientFactory;
    private readonly IGenerateApiResourceRefs _refGenerator;
    private readonly ILogger<GetFranchiseByIdQueryHandler> _logger;

    public GetFranchiseByIdQueryHandler(
        IFranchiseClientFactory franchiseClientFactory,
        IGenerateApiResourceRefs refGenerator,
        ILogger<GetFranchiseByIdQueryHandler> logger)
    {
        _franchiseClientFactory = franchiseClientFactory;
        _refGenerator = refGenerator;
        _logger = logger;
    }

    public async Task<Result<FranchiseResponseDto>> ExecuteAsync(
        GetFranchiseByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("GetFranchiseById query: sport={Sport}, league={League}, idOrSlug={IdOrSlug}",
            query.Sport, query.League, query.Id);

        // Resolve the appropriate client for this sport/league
        IProvideFranchises client;
        try
        {
            client = _franchiseClientFactory.Resolve(query.Sport, query.League);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(ex,
                "Unsupported sport/league combination. Sport={Sport}, League={League}",
                query.Sport, query.League);
            return new Failure<FranchiseResponseDto>(
                default!,
                ResultStatus.BadRequest,
                [new ValidationFailure("Sport/League", ex.Message)]);
        }

        // Get canonical response from Producer
        var franchiseResult = await client.GetFranchiseById(query.Id);

        if (franchiseResult is Failure<GetFranchiseByIdResponse> failure)
        {
            _logger.LogWarning(
                "GetFranchiseById failed. Sport={Sport}, League={League}, IdOrSlug={IdOrSlug}, Status={Status}",
                query.Sport,
                query.League,
                query.Id,
                failure.Status);
            
            return new Failure<FranchiseResponseDto>(
                default!,
                failure.Status,
                failure.Errors);
        }

        var canonical = (franchiseResult as Success<GetFranchiseByIdResponse>)!.Value.Franchise!;
        var sport = query.Sport;
        var league = query.League;

        // Enrich with HATEOAS
        var enrichedResponse = EnrichFranchise(canonical, sport, league);

        return new Success<FranchiseResponseDto>(enrichedResponse);
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
