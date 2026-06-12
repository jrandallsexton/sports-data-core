using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.UI.Results.Dtos;
using SportsData.Api.Application.UI.Results.Queries.GetSeasonResults;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.UI.Results;

[ApiController]
[Route("ui/results/sport/{sport}/league/{league}/{seasonYear}")]
[AllowAnonymous]
public class ResultsController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SeasonResultsDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SeasonResultsDto>> GetSeasonResults(
        string sport,
        string league,
        int seasonYear,
        [FromServices] IGetSeasonResultsQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetSeasonResultsQuery
        {
            Sport = sport,
            League = league,
            SeasonYear = seasonYear
        };

        var result = await handler.ExecuteAsync(query, cancellationToken);
        return result.ToActionResult();
    }
}
