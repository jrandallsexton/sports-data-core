using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;
using SportsData.Producer.Application.Venues.Queries.GetAllVenues;
using SportsData.Producer.Application.Venues.Queries.GetVenueById;

namespace SportsData.Producer.Application.Venues;

[Route("api/venues")]
[ApiController]
public class VenuesController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<GetAllVenuesResponse>> GetVenues(
        [FromServices] IGetAllVenuesQueryHandler handler,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAllVenuesQuery
        {
            PageNumber = pageNumber,
            PageSize = pageSize
        };
        
        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<VenueDto>> GetVenueById(
        [FromServices] IGetVenueByIdentifierQueryHandler handler,
        [FromRoute] string id,
        CancellationToken cancellationToken = default)
    {
        var query = new GetVenueByIdQuery(id);
        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }
}
