using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.UI.TeamCard.Queries.GetTeamCard;
using SportsData.Api.Application.UI.TeamCard.Queries.GetTeamMetrics;
using SportsData.Api.Application.UI.TeamCard.Queries.GetTeamStatistics;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Common;
using SportsData.Core.Common.Mapping;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Franchise;

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

    [HttpGet("logos")]
    public async Task<ActionResult<FranchiseLogosDto>> GetFranchiseLogos(
        string sport,
        string league,
        string slug,
        [FromServices] IFranchiseClientFactory franchiseClientFactory,
        CancellationToken cancellationToken)
    {
        var mode = ModeMapper.ResolveMode(sport, league);
        var client = franchiseClientFactory.Resolve(mode);
        var result = await client.GetFranchiseLogos(slug, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPatch("logos/{logoId}/dark-bg")]
    public async Task<ActionResult<bool>> UpdateLogoDarkBg(
        string sport,
        string league,
        string slug,
        [FromRoute] Guid logoId,
        [FromBody] UpdateLogoDarkBgRequest request,
        [FromServices] IFranchiseClientFactory franchiseClientFactory,
        CancellationToken cancellationToken)
    {
        var mode = ModeMapper.ResolveMode(sport, league);
        var client = franchiseClientFactory.Resolve(mode);
        var result = await client.UpdateLogoDarkBg(logoId, request.IsForDarkBg, request.LogoType, cancellationToken);
        return result.ToActionResult();
    }
}

public record UpdateLogoDarkBgRequest(bool IsForDarkBg, string LogoType);
