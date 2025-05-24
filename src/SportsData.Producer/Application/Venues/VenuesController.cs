using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Common;
using SportsData.Producer.Application.Venues.Queries;

namespace SportsData.Producer.Application.Venues
{
    [Route("api/venues")]
    public class VenuesController : ApiControllerBase
    {
        private readonly ILogger<VenuesController> _logger;

        public VenuesController(ILogger<VenuesController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetVenues()
        {
            return Ok(await Mediator.Send(new GetVenues.Query()));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetVenue(string id)
        {
            _logger.LogInformation("Request began with {@Id}", id);
            return Ok(await Mediator.Send(new GetVenueById.Query(Guid.Parse(id))));
        }
    }
}
