using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Common;
using SportsData.Venue.Application.Queries;

namespace SportsData.Venue.Application
{
    public class VenueController : ApiControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> GetCandidates()
        {
            return Ok(await Mediator.Send(new GetVenues.Query()));
        }
    }
}
