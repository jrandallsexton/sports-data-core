using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Venue.Queries;

namespace SportsData.Api.Application.UI.Venues;

[ApiController]
[Route("api/sports/{sport}/leagues/{league}/venues")]
public class VenuesController : ApiControllerBase
{
    private readonly IVenueClientFactory _venueClientFactory;
    //private readonly IDistributedCache _cache;
    private readonly IDateTimeProvider _dateTimeProvider;

    private const string CacheKeyDateTimeFormat = "yyyyMMdd_hhmm";

    public VenuesController(
        IVenueClientFactory venueClientFactory,
        //IDistributedCache cache,
        IDateTimeProvider dateTimeProvider)
    {
        _venueClientFactory = venueClientFactory;
        //_cache = cache;
        _dateTimeProvider = dateTimeProvider;
    }

    [HttpGet(Name = "GetVenues")]
    [Produces<GetVenuesResponse>]
    public async Task<ActionResult<GetVenuesResponse>> GetVenues(
        [FromRoute] string sport,
        [FromRoute] string league)
    {
        //var cacheKey = $"{nameof(GetVenuesResponse)}_{_dateTimeProvider.UtcNow().ToString(CacheKeyDateTimeFormat)}";
        //var cached = await _cache.GetRecordAsync<GetVenuesResponse>(cacheKey);

        //if (cached?.Venues != null)
        //    return Ok(cached);

        var client = _venueClientFactory.Resolve(sport, league);
        var venues = await client.GetVenues();

        if (venues is Failure<GetVenuesResponse> failure)
        {
            return MapFailure(failure);
        }

        //await _cache.SetRecordAsync(cacheKey, venues.Value);
        return Ok(venues.Value);
    }

    [HttpGet("{id}")]
    [Produces<GetVenueByIdResponse>]
    public async Task<ActionResult<GetVenueByIdResponse>> GetVenueById(
        [FromRoute] string sport,
        [FromRoute] string league,
        [FromRoute] string id)
    {
        var client = _venueClientFactory.Resolve(sport, league);
        var venues = await client.GetVenueById(id);

        return venues is Success<GetVenueByIdResponse> success ?
            MapSuccess(success) :
            MapFailure(venues as Failure<GetVenueByIdResponse>);
    }
}
