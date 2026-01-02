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

    /// <summary>
    /// Initializes a new instance of <see cref="VenuesController"/> with its required dependencies.
    /// </summary>
    /// <param name="venueClientFactory">Factory used to resolve a venue client for the requested sport and league.</param>
    /// <param name="dateTimeProvider">Provider for current date and time values.</param>
    public VenuesController(
        IVenueClientFactory venueClientFactory,
        //IDistributedCache cache,
        IDateTimeProvider dateTimeProvider)
    {
        _venueClientFactory = venueClientFactory;
        //_cache = cache;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <summary>
    /// Retrieve the list of venues for the specified sport and league.
    /// </summary>
    /// <param name="sport">Identifier of the sport used to select the venue provider.</param>
    /// <param name="league">Identifier of the league within the sport.</param>
    /// <returns>The requested GetVenuesResponse containing the venues when successful; otherwise an appropriate failure result is returned.</returns>
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

    /// <summary>
    /// Gets a venue by its identifier for the specified sport and league.
    /// </summary>
    /// <param name="sport">The sport identifier used to resolve the venue client.</param>
    /// <param name="league">The league identifier used to resolve the venue client.</param>
    /// <param name="id">The unique identifier of the venue to retrieve.</param>
    /// <returns>
    /// A <see cref="GetVenueByIdResponse"/> containing the venue when successful; otherwise an appropriate HTTP error response.
    /// </returns>
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