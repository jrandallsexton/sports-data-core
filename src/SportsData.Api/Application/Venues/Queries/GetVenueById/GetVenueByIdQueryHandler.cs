using FluentValidation.Results;
using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Venue;
using SportsData.Core.Infrastructure.Clients.Venue.Queries;
using SportsData.Api.Infrastructure.Refs;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Api.Application.Venues.Queries.GetVenueById;

public interface IGetVenueByIdQueryHandler
{
    Task<Result<VenueResponseDto>> ExecuteAsync(
        GetVenueByIdQuery query,
        CancellationToken cancellationToken = default);
}

public class GetVenueByIdQueryHandler : IGetVenueByIdQueryHandler
{
    private readonly IVenueClientFactory _venueClientFactory;
    private readonly IGenerateApiResourceRefs _refGenerator;
    private readonly ILogger<GetVenueByIdQueryHandler> _logger;

    public GetVenueByIdQueryHandler(
        IVenueClientFactory venueClientFactory,
        IGenerateApiResourceRefs refGenerator,
        ILogger<GetVenueByIdQueryHandler> logger)
    {
        _venueClientFactory = venueClientFactory;
        _refGenerator = refGenerator;
        _logger = logger;
    }

    public async Task<Result<VenueResponseDto>> ExecuteAsync(
        GetVenueByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "GetVenueById started. Sport={Sport}, League={League}, Id={Id}",
            query.Sport,
            query.League,
            query.Id);

        // Get canonical data from internal service (Producer)
        IProvideVenues client;
        try
        {
            client = _venueClientFactory.Resolve(query.Sport, query.League);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(ex,
                "Unsupported sport/league combination. Sport={Sport}, League={League}",
                query.Sport, query.League);
            return new Failure<VenueResponseDto>(
                null!,
                ResultStatus.BadRequest,
                [new ValidationFailure("Sport/League", ex.Message)]);
        }

        var venueResult = await client.GetVenueById(query.Id, cancellationToken);

        if (venueResult is Failure<GetVenueByIdResponse> failure)
        {
            _logger.LogWarning(
                "GetVenueById failed. Sport={Sport}, League={League}, Id={Id}, Status={Status}",
                query.Sport,
                query.League,
                query.Id,
                failure.Status);

            return new Failure<VenueResponseDto>(
                null!,
                failure.Status,
                failure.Errors);
        }

        var venue = (venueResult as Success<GetVenueByIdResponse>)?.Value.Venue;
        if (venue == null)
        {
            _logger.LogWarning(
                "GetVenueById - venue not found. Sport={Sport}, League={League}, Id={Id}",
                query.Sport,
                query.League,
                query.Id);

            return new Failure<VenueResponseDto>(
                null!,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(query.Id), $"Venue not found with id: {query.Id}")]);
        }

        // Enrich with HATEOAS
        var enrichedVenue = EnrichVenue(venue, query.Sport, query.League);

        _logger.LogInformation(
            "GetVenueById completed. Sport={Sport}, League={League}, Id={Id}, Name={Name}",
            query.Sport,
            query.League,
            query.Id,
            venue.Name);

        return new Success<VenueResponseDto>(enrichedVenue);
    }

    /// <summary>
    /// Transforms canonical VenueDto into enriched VenueResponseDto with HATEOAS refs.
    /// </summary>
    private VenueResponseDto EnrichVenue(SportsData.Core.Dtos.Canonical.VenueDto canonical, string sport, string league)
    {
        return new VenueResponseDto
        {
            Id = canonical.Id,
            Name = canonical.Name,
            ShortName = canonical.ShortName,
            IsGrass = canonical.IsGrass,
            IsIndoor = canonical.IsIndoor,
            Slug = canonical.Slug,
            Capacity = canonical.Capacity,
            Images = canonical.Images,
            Address = canonical.Address,
            Latitude = canonical.Latitude,
            Longitude = canonical.Longitude,

            // Add HATEOAS refs
            Ref = _refGenerator.ForVenue(canonical.Id, sport, league),
            Links = new Dictionary<string, Uri>
            {
                ["self"] = _refGenerator.ForVenue(canonical.Id, sport, league)
                // Add more navigation links as needed:
                // ["events"] = new Uri($"{baseUrl}/api/{sport}/{league}/venues/{canonical.Id}/events"),
                // ["teams"] = new Uri($"{baseUrl}/api/{sport}/{league}/venues/{canonical.Id}/teams")
            }
        };
    }
}
