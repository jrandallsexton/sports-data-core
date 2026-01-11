using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.Venues.Queries.GetVenueById;
using SportsData.Api.Application.Venues.Queries.GetVenues;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.Venues;

[ApiController]
[Route("api/{sport}/{league}/venues")]
public class VenuesController : ApiControllerBase
{
    [HttpGet(Name = "GetVenues")]
    [ProducesResponseType(typeof(GetVenuesResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<GetVenuesResponseDto>> GetVenues(
        [FromServices] IGetVenuesQueryHandler handler,
        [FromRoute] string sport,
        [FromRoute] string league,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new GetVenuesQuery
        {
            Sport = sport,
            League = league,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(VenueResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<VenueResponseDto>> GetVenueById(
        [FromServices] IGetVenueByIdQueryHandler handler,
        [FromRoute] string sport,
        [FromRoute] string league,
        [FromRoute] string id,
        CancellationToken cancellationToken = default)
    {
        var query = new GetVenueByIdQuery
        {
            Sport = sport,
            League = league,
            Id = id
        };

        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }
}
