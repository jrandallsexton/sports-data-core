using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.UI.Leagues.Commands.AddMatchup;
using SportsData.Api.Application.UI.Leagues.Commands.CreateLeague;
using SportsData.Api.Application.UI.Leagues.Commands.CreateLeague.Dtos;
using SportsData.Api.Application.UI.Leagues.Commands.DeleteLeague;
using SportsData.Api.Application.UI.Leagues.Commands.GenerateLeagueWeekPreviews;
using SportsData.Api.Application.UI.Leagues.Commands.JoinLeague;
using SportsData.Api.Application.UI.Leagues.Commands.SendLeagueInvite;
using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueById;
using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueScoresByWeek;
using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekMatchups;
using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekOverview;
using SportsData.Api.Application.UI.Leagues.Queries.GetPublicLeagues;
using SportsData.Api.Application.UI.Leagues.Queries.GetUserLeagues;
using SportsData.Api.Extensions;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.UI.Leagues;

[ApiController]
[Route("ui/leagues")]
public class LeagueController : ApiControllerBase
{
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<Guid>> Create(
        [FromBody] CreateLeagueRequest request,
        [FromServices] ICreateLeagueCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetCurrentUserId();

        var result = await handler.ExecuteAsync(request, userId, cancellationToken);

        if (result.IsSuccess)
            return CreatedAtAction(nameof(GetById), new { id = result.Value }, new { id = result.Value });

        return result.ToActionResult();
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<LeagueDetailDto>> GetById(
        Guid id,
        [FromServices] IGetLeagueByIdQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetLeagueByIdQuery { LeagueId = id };
        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<List<LeagueSummaryDto>>> GetLeagues(
        [FromServices] IGetUserLeaguesQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetCurrentUserId();

        var query = new GetUserLeaguesQuery { UserId = userId };
        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }

    [HttpPost("{id}/join")]
    [Authorize]
    public async Task<ActionResult<Guid?>> JoinLeague(
        [FromRoute] string id,
        [FromServices] IJoinLeagueCommandHandler handler,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var leagueId))
            return BadRequest("Invalid league ID format.");

        var userId = HttpContext.GetCurrentUserId();

        var command = new JoinLeagueCommand
        {
            PickemGroupId = leagueId,
            UserId = userId
        };

        var result = await handler.ExecuteAsync(command, cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("{id}/matchups/{week}")]
    [Authorize]
    public async Task<ActionResult<LeagueWeekMatchupsDto>> GetMatchupsForLeagueWeek(
        [FromRoute] Guid id,
        [FromRoute] int week,
        [FromServices] IGetLeagueWeekMatchupsQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetCurrentUserId();

        var query = new GetLeagueWeekMatchupsQuery
        {
            UserId = userId,
            LeagueId = id,
            Week = week
        };

        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<ActionResult<Guid>> Delete(
        [FromRoute] Guid id,
        [FromServices] IDeleteLeagueCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetCurrentUserId();

        var command = new DeleteLeagueCommand
        {
            UserId = userId,
            LeagueId = id
        };

        var result = await handler.ExecuteAsync(command, cancellationToken);

        if (result.IsSuccess)
            return NoContent();

        return result.ToActionResult();
    }

    [HttpPost("{id}/invite")]
    [Authorize]
    public async Task<IActionResult> SendInvite(
        Guid id,
        [FromBody] SendLeagueInviteRequest request,
        [FromServices] ISendLeagueInviteCommandHandler handler,
        CancellationToken cancellationToken)
    {
        if (id != request.LeagueId)
            return BadRequest("Mismatched league ID in route vs body.");

        var command = new SendLeagueInviteCommand
        {
            LeagueId = id,
            Email = request.Email,
            InviteeName = request.InviteeName
        };

        var result = await handler.ExecuteAsync(command, cancellationToken);

        if (result.IsSuccess)
            return Ok(new { Message = "Invite sent." });

        return result.Status switch
        {
            ResultStatus.NotFound => NotFound(),
            _ => BadRequest()
        };
    }

    [HttpGet("discover")]
    [Authorize]
    public async Task<ActionResult<List<PublicLeagueDto>>> GetPublicLeagues(
        [FromServices] IGetPublicLeaguesQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetCurrentUserId();

        var query = new GetPublicLeaguesQuery { UserId = userId };
        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("{id}/overview/{week}")]
    [Authorize]
    public async Task<ActionResult<LeagueWeekOverviewDto>> GetLeagueWeekOverview(
        [FromRoute] Guid id,
        [FromRoute] int week,
        [FromServices] IGetLeagueWeekOverviewQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetLeagueWeekOverviewQuery
        {
            LeagueId = id,
            Week = week
        };
        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }

    [HttpPost("{id}/previews/{weekId}/generate")]
    public async Task<ActionResult<Guid>> GenerateMatchupPreviews(
        [FromRoute] Guid id,
        [FromRoute] int weekId,
        [FromServices] IGenerateLeagueWeekPreviewsCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new GenerateLeagueWeekPreviewsCommand
        {
            LeagueId = id,
            WeekNumber = weekId
        };
        var result = await handler.ExecuteAsync(command, cancellationToken);

        if (result.IsSuccess)
            return Accepted(new { correlationId = result.Value });

        return result.ToActionResult();
    }

    /// <summary>
    /// Gets scores by week for all members of a league.
    /// </summary>
    /// <param name="id">The league ID</param>
    /// <param name="handler">The query handler</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Weekly scores for all league members</returns>
    [HttpGet("{id}/scores")]
    [Authorize]
    public async Task<ActionResult<LeagueScoresByWeekDto>> GetLeagueScoresByWeek(
        [FromRoute] Guid id,
        [FromServices] IGetLeagueScoresByWeekQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetLeagueScoresByWeekQuery { LeagueId = id };
        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }

    /// <summary>
    /// Adds a single matchup to a league. Only the league commissioner can add matchups.
    /// This is used for post-season games that are not known at the time of matchup generation.
    /// </summary>
    /// <param name="id">The league ID</param>
    /// <param name="command">The command containing the contest ID to add</param>
    /// <param name="handler">The command handler</param>
    /// <param name="logger">The logger</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The ID of the newly created matchup</returns>
    [HttpPost("{id}/matchups")]
    [Authorize]
    public async Task<ActionResult<Guid>> AddMatchup(
        [FromRoute] Guid id,
        [FromBody] AddMatchupCommand command,
        [FromServices] IAddMatchupCommandHandler handler,
        [FromServices] ILogger<LeagueController> logger,
        CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetCurrentUserId();

        // Hydrate command with route context
        var hydratedCommand = new AddMatchupCommand
        {
            LeagueId = id,
            ContestId = command.ContestId,
            CorrelationId = command.CorrelationId
        };

        logger.LogInformation(
            "AddMatchup endpoint called. LeagueId={LeagueId}, ContestId={ContestId}, UserId={UserId}",
            id, command.ContestId, userId);

        var result = await handler.ExecuteAsync(hydratedCommand, userId, cancellationToken);

        if (result.IsSuccess)
            return CreatedAtAction(
                nameof(GetMatchupsForLeagueWeek),
                new { id, week = 0 }, // Week is not known here, but route needs it
                new { matchupId = result.Value });

        return result.ToActionResult();
    }
}
