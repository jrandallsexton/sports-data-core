using SportsData.Api.Infrastructure.Refs;
using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Venue;
using SportsData.Core.Infrastructure.Clients.Venue.Queries;

namespace SportsData.Api.Application.Venues.Queries.GetVenues;

public interface IGetVenuesQueryHandler
{
    Task<Result<GetVenuesResponseDto>> ExecuteAsync(
        GetVenuesQuery query,
        CancellationToken cancellationToken = default);
}

public class GetVenuesQueryHandler : IGetVenuesQueryHandler
{
    private readonly IVenueClientFactory _venueClientFactory;
    private readonly IGenerateApiResourceRefs _refGenerator;
    private readonly ILogger<GetVenuesQueryHandler> _logger;

    public GetVenuesQueryHandler(
        IVenueClientFactory venueClientFactory,
        IGenerateApiResourceRefs refGenerator,
        ILogger<GetVenuesQueryHandler> logger)
    {
        _venueClientFactory = venueClientFactory;
        _refGenerator = refGenerator;
        _logger = logger;
    }

    public async Task<Result<GetVenuesResponseDto>> ExecuteAsync(
        GetVenuesQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "GetVenues started. Sport={Sport}, League={League}, PageNumber={PageNumber}, PageSize={PageSize}",
            query.Sport,
            query.League,
            query.PageNumber,
            query.PageSize);

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
            return new Failure<GetVenuesResponseDto>(
                new GetVenuesResponseDto(),
                ResultStatus.BadRequest,
                [new FluentValidation.Results.ValidationFailure("Sport/League", ex.Message)]);
        }

        var venuesResult = await client.GetVenues(query.PageNumber, query.PageSize, cancellationToken);

        if (venuesResult is Failure<GetVenuesResponse> failure)
        {
            _logger.LogWarning(
                "GetVenues failed. Sport={Sport}, League={League}, Status={Status}",
                query.Sport,
                query.League,
                failure.Status);
            
            return new Failure<GetVenuesResponseDto>(
                new GetVenuesResponseDto(),
                failure.Status,
                failure.Errors);
        }

        var canonicalResponse = venuesResult.Value;

        // Enrich with HATEOAS
        var enrichedResponse = new GetVenuesResponseDto
        {
            Items = canonicalResponse.Items.Select(v => EnrichVenue(v, query.Sport, query.League)).ToList(),

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
                ["self"] = _refGenerator.ForVenues(query.Sport, query.League, canonicalResponse.PageNumber, canonicalResponse.PageSize),
                ["first"] = _refGenerator.ForVenues(query.Sport, query.League, 1, canonicalResponse.PageSize),
                ["last"] = _refGenerator.ForVenues(query.Sport, query.League,
                    canonicalResponse.TotalPages > 0 ? canonicalResponse.TotalPages : 1,
                    canonicalResponse.PageSize)
            }
        };

        // Add prev link if not on first page
        if (canonicalResponse.HasPreviousPage)
        {
            enrichedResponse.Links["prev"] = _refGenerator.ForVenues(
                query.Sport, query.League,
                canonicalResponse.PageNumber - 1,
                canonicalResponse.PageSize);
        }

        // Add next link if not on last page
        if (canonicalResponse.HasNextPage)
        {
            enrichedResponse.Links["next"] = _refGenerator.ForVenues(
                query.Sport, query.League,
                canonicalResponse.PageNumber + 1,
                canonicalResponse.PageSize);
        }

        _logger.LogInformation(
            "GetVenues completed. Sport={Sport}, League={League}, TotalCount={TotalCount}, ItemsReturned={ItemsReturned}",
            query.Sport,
            query.League,
            canonicalResponse.TotalCount,
            enrichedResponse.Items.Count);

        return new Success<GetVenuesResponseDto>(enrichedResponse);
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
