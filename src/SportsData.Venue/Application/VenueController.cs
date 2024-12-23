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
    }
}
