using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.Franchises.Queries.GetFranchiseById;
using SportsData.Api.Application.Franchises.Queries.GetFranchises;
using SportsData.Api.Application.Franchises.Seasons;
using SportsData.Api.Application.Franchises.Seasons.Contests;
using SportsData.Api.Application.Franchises.Seasons.Queries.GetFranchiseSeasonById;
using SportsData.Api.Application.Franchises.Seasons.Queries.GetFranchiseSeasons;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.Franchises;

[ApiController]
[Route("api/{sport}/{league}/franchises")]
public class FranchisesController : ApiControllerBase
{
    [HttpGet(Name = "GetFranchises")]
    [ProducesResponseType(typeof(GetFranchisesResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<GetFranchisesResponseDto>> GetFranchises(
        [FromServices] IGetFranchisesQueryHandler handler,
        [FromRoute] string sport,
        [FromRoute] string league,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new GetFranchisesQuery(sport, league, pageNumber, pageSize);
        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("{franchiseIdOrSlug}")]
    [ProducesResponseType(typeof(FranchiseResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FranchiseResponseDto>> GetFranchiseById(
        [FromServices] IGetFranchiseByIdQueryHandler handler,
        [FromRoute] string sport,
        [FromRoute] string league,
        [FromRoute] string franchiseIdOrSlug,
        CancellationToken cancellationToken = default)
    {
        var query = new GetFranchiseByIdQuery(sport, league, franchiseIdOrSlug);
        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("{franchiseIdOrSlug}/seasons")]
    [ProducesResponseType(typeof(GetFranchiseSeasonsResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<GetFranchiseSeasonsResponseDto>> GetFranchiseSeasons(
        [FromServices] IGetFranchiseSeasonsQueryHandler handler,
        [FromRoute] string sport,
        [FromRoute] string league,
        [FromRoute] string franchiseIdOrSlug,
        CancellationToken cancellationToken = default)
    {
        var query = new GetFranchiseSeasonsQuery(sport, league, franchiseIdOrSlug);
        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("{franchiseIdOrSlug}/seasons/{seasonYear}")]
    [ProducesResponseType(typeof(FranchiseSeasonResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FranchiseSeasonResponseDto>> GetFranchiseSeasonById(
        [FromServices] IGetFranchiseSeasonByIdQueryHandler handler,
        [FromRoute] string sport,
        [FromRoute] string league,
        [FromRoute] string franchiseIdOrSlug,
        [FromRoute] int seasonYear,
        CancellationToken cancellationToken = default)
    {
        var query = new GetFranchiseSeasonByIdQuery(sport, league, franchiseIdOrSlug, seasonYear);
        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("{franchiseIdOrSlug}/seasons/{seasonYear}/contests")]
    [ProducesResponseType(typeof(GetSeasonContestsResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<GetSeasonContestsResponseDto>> GetSeasonContests(
        [FromServices] IGetSeasonContestsQueryHandler handler,
        [FromRoute] string sport,
        [FromRoute] string league,
        [FromRoute] string franchiseIdOrSlug,
        [FromRoute] int seasonYear,
        [FromQuery] int? week,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new GetSeasonContestsQuery(sport, league, franchiseIdOrSlug, seasonYear, week, pageNumber, pageSize);
        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }
}
