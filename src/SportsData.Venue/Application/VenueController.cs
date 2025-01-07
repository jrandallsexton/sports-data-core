using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Common;
using SportsData.Venue.Application.Queries;

namespace SportsData.Venue.Application
{
    [Route("api/venue")]
    public class VenueController : ApiControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> GetVenues()
        {
            return Ok(await Mediator.Send(new GetVenues.Query()));
        }

        [HttpGet("{id:int}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<GetVenueById.Dto>> GetVenue(int id)
        {
            return await Send<GetVenueById.Query, GetVenueById.Dto>(
                new GetVenueById.Query() { Id = id });
        }
    }
}
