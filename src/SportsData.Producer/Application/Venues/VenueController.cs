using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Common;
using SportsData.Producer.Application.Venues.Queries;

namespace SportsData.Producer.Application.Venues
{
    [Route("api/venue")]
    public class VenueController : ApiControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> GetVenues()
        {
            return Ok(await Mediator.Send(new GetVenues.Query()));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetVenue(string id)
        {
            return Ok(await Mediator.Send(new GetVenueById.Query(Guid.Parse(id))));
        }
    }
}
