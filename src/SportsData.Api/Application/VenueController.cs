using Microsoft.AspNetCore.Mvc;

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
            return Ok(await _provider.GetVenues());
        }
    }
}
