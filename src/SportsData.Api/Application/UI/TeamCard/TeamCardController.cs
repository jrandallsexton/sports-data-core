using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.UI.TeamCard.Dtos;
using SportsData.Api.Application.UI.TeamCard.Queries.GetTeamCard;
using SportsData.Api.Application.UI.TeamCard.Queries.GetTeamMetrics;
using SportsData.Api.Application.UI.TeamCard.Queries.GetTeamStatistics;
using SportsData.Api.Infrastructure.Data.Canonical.Models;
using SportsData.Core.Common;
using SportsData.Core.Common.Mapping;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;

using SportsData.Api.Application.Common.Enums;

namespace SportsData.Api.Application.UI.TeamCard;

[ApiController]
[Route("ui/teamcard/sport/{sport}/league/{league}/team/{slug}/{seasonYear}")]
public class TeamCardController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(TeamCardDto))]
    public async Task<ActionResult<TeamCardDto>> GetTeamCard(
        string sport,
        string league,
        string slug,
        int seasonYear,
        [FromServices] IGetTeamCardQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetTeamCardQuery
        {
            Sport = sport,
            League = league,
            Slug = slug,
            SeasonYear = seasonYear
        };

        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("statistics")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FranchiseSeasonStatisticDto))]
    public async Task<ActionResult<FranchiseSeasonStatisticDto>> GetTeamStatistics(
        string sport,
        string league,
        string slug,
        int seasonYear,
        [FromQuery] Guid franchiseSeasonId,
        [FromServices] IGetTeamStatisticsQueryHandler handler,
        CancellationToken cancellationToken)
    {
        // TODO: Rework this to get the franchiseSeasonId from the other parameters
        var result = await handler.ExecuteAsync(
            new GetTeamStatisticsQuery { FranchiseSeasonId = franchiseSeasonId },
            cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("metrics")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FranchiseSeasonMetricsDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FranchiseSeasonMetricsDto>> GetTeamMetrics(
        string sport,
        string league,
        string slug,
        int seasonYear,
        [FromQuery] Guid franchiseSeasonId,
        [FromServices] IGetTeamMetricsQueryHandler handler,
        CancellationToken cancellationToken)
    {
        // TODO: Rework this to get the franchiseSeasonId from the other parameters
        Sport mode;
        try
        {
            mode = ModeMapper.ResolveMode(sport, league);
        }
        catch (NotSupportedException)
        {
            return BadRequest($"Unsupported sport/league combination: {sport}/{league}");
        }

        var result = await handler.ExecuteAsync(
            new GetTeamMetricsQuery { FranchiseSeasonId = franchiseSeasonId, Sport = mode },
            cancellationToken);

        return result.ToActionResult();
    }
}
