using Microsoft.AspNetCore.Mvc;
using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Venue;
using SportsData.Core.Infrastructure.Clients.Venue.Queries;

namespace SportsData.Api.Application
{
    [ApiController]
    [Route("[controller]")]
    public class VenueController : ControllerBase
    {
        private readonly IProvideVenues _provider;

        public VenueController(IProvideVenues provider)
        {
            _provider = provider;
        }

        [HttpGet(Name = "GetVenues")]
        [Produces<GetVenuesResponse>]
        public async Task<IActionResult> GetVenues()
        {
            var venues = await _provider.GetVenues();
            return Ok(venues.Value.Venues);
        }

        [HttpGet("{id}")]
        [Produces<GetVenueByIdResponse>]
        public async Task<IActionResult> GetVenueById(int id)
        {
            var venues = await _provider.GetVenueById(id);
            if (venues is Success<GetVenueByIdResponse>)
            {
                return Ok(venues.Value.Venue);
            }
            return NotFound();
        }
    }
}
