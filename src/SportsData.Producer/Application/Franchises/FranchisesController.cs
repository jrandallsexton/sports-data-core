using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Contest.Queries;
using SportsData.Producer.Application.Franchises.Queries.GetAllFranchises;
using SportsData.Producer.Application.Franchises.Queries.GetFranchiseById;
using SportsData.Producer.Application.Franchises.Queries.GetFranchiseSeasons;
using SportsData.Producer.Application.Franchises.Queries.GetSeasonContests;
using SportsData.Producer.Application.FranchiseSeasons.Queries.GetFranchiseSeasonById;

namespace SportsData.Producer.Application.Franchises;

[Route("api/franchises")]
[ApiController]
public class FranchisesController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<GetAllFranchisesResponse>> GetFranchises(
        [FromServices] IGetAllFranchisesQueryHandler handler,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAllFranchisesQuery
        {
            PageNumber = pageNumber,
            PageSize = pageSize
        };
        
        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<FranchiseDto>> GetFranchiseById(
        [FromServices] IGetFranchiseByIdQueryHandler handler,
        [FromRoute] string id,
        CancellationToken cancellationToken = default)
    {
        var query = new GetFranchiseByIdQuery(id);
        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("{franchiseId}/seasons")]
    public async Task<ActionResult<List<FranchiseSeasonDto>>> GetFranchiseSeasons(
        [FromServices] IGetFranchiseSeasonsQueryHandler handler,
        [FromRoute] Guid franchiseId,
        CancellationToken cancellationToken = default)
    {
        var query = new GetFranchiseSeasonsQuery(franchiseId);
        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("{franchiseId}/seasons/{seasonYear}")]
    public async Task<ActionResult<FranchiseSeasonDto>> GetFranchiseSeasonById(
        [FromServices] IGetFranchiseSeasonByIdQueryHandler handler,
        [FromRoute] Guid franchiseId,
        [FromRoute] int seasonYear,
        CancellationToken cancellationToken = default)
    {
        var query = new GetFranchiseSeasonByIdQuery(franchiseId, seasonYear);
        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("{franchiseId}/seasons/{seasonYear}/contests")]
    public async Task<ActionResult<List<SeasonContestDto>>> GetSeasonContests(
        [FromServices] IGetSeasonContestsQueryHandler handler,
        [FromRoute] Guid franchiseId,
        [FromRoute] int seasonYear,
        [FromQuery] int? week = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new GetSeasonContestsQuery(franchiseId, seasonYear, week, pageNumber, pageSize);
        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }
}
