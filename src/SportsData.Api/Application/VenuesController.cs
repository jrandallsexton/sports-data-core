using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;

using SportsData.Core.Common;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Venue;
using SportsData.Core.Infrastructure.Clients.Venue.Queries;

namespace SportsData.Api.Application
{
    [ApiController]
    [Route("[controller]")]
    public class VenuesController : ApiControllerBase
    {
        private readonly IProvideVenues _provider;
        private readonly IDistributedCache _cache;
        private readonly IDateTimeProvider _dateTimeProvider;

        private const string CacheKeyDateTimeFormat = "yyyyMMdd_hhmm";

        public VenuesController(
            IProvideVenues provider,
            IDistributedCache cache,
            IDateTimeProvider dateTimeProvider)
        {
            _provider = provider;
            _cache = cache;
            _dateTimeProvider = dateTimeProvider;
        }

        [HttpGet(Name = "GetVenues")]
        [Produces<GetVenuesResponse>]
        public async Task<ActionResult<GetVenuesResponse>> GetVenues()
        {
            var cacheKey = $"{nameof(GetVenuesResponse)}_{_dateTimeProvider.UtcNow().ToString(CacheKeyDateTimeFormat)}";
            var cached = await _cache.GetRecordAsync<GetVenuesResponse>(cacheKey);

            if (cached?.Venues != null)
                return Ok(cached);

            var venues = await _provider.GetVenues();
            await _cache.SetRecordAsync(cacheKey, venues.Value);
            return Ok(venues.Value);
        }

        [HttpGet("{Id}")]
        [Produces<GetVenueByIdResponse>]
        public async Task<ActionResult<GetVenueByIdResponse>> GetVenueById(int id)
        {
            var venues = await _provider.GetVenueById(id);

            return venues is Success<GetVenueByIdResponse> success ?
                MapSuccess(success) :
                MapFailure(venues as Failure<GetVenueByIdResponse>);
        }
    }
}
