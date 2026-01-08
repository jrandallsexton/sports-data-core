using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;
using SportsData.Producer.Application.Venues.Queries.GetAllVenues;
using SportsData.Producer.Application.Venues.Queries.GetVenueByIdentifier;

namespace SportsData.Producer.Application.Venues;

[Route("api/venues")]
[ApiController]
public class VenuesController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<VenueDto>>> GetVenues(
        [FromServices] IGetAllVenuesQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetAllVenuesQuery();
        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<VenueDto>> GetVenueById(
        [FromRoute] string id,
        [FromServices] IGetVenueByIdentifierQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetVenueByIdentifierQuery(id);
        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }
}
