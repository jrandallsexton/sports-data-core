using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.Athletes.Queries.GetAthleteDetails;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.Athletes;

[ApiController]
[Route("api/{sport}/{league}/athletes")]
public class AthletesController : ApiControllerBase
{
    /// <summary>
    /// Full athlete drill-down for the web athlete page: athlete record,
    /// every season, and per-season statistic documents. GUID route — the
    /// slug convention assumes uniqueness athlete slugs don't have.
    /// </summary>
    [HttpGet("{athleteId:guid}")]
    public async Task<ActionResult<AthleteDetailDto>> GetAthleteDetails(
        [FromRoute] string sport,
        [FromRoute] string league,
        [FromRoute] Guid athleteId,
        [FromServices] IGetAthleteDetailsQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(
            new GetAthleteDetailsQuery(sport, league, athleteId),
            cancellationToken);
        return result.ToActionResult();
    }
}
