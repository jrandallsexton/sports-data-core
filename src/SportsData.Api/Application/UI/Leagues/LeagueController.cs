using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.UI.Leagues.Commands.AddMatchup;
using SportsData.Api.Application.UI.Leagues.Commands.CreateBaseballMlbLeague;
using SportsData.Api.Application.UI.Leagues.Commands.CreateBaseballMlbLeague.Dtos;
using SportsData.Api.Application.UI.Leagues.Commands.CreateFootballNcaaLeague;
using SportsData.Api.Application.UI.Leagues.Commands.CreateFootballNcaaLeague.Dtos;
using SportsData.Api.Application.UI.Leagues.Commands.CreateFootballNflLeague;
using SportsData.Api.Application.UI.Leagues.Commands.CreateFootballNflLeague.Dtos;
using SportsData.Api.Application.UI.Leagues.Commands.DeleteLeague;
using SportsData.Api.Application.UI.Leagues.Commands.GenerateLeagueWeekPreviews;
using SportsData.Api.Application.UI.Leagues.Commands.InviteUserToLeague;
using SportsData.Api.Application.UI.Leagues.Commands.JoinLeague;
using SportsData.Api.Application.UI.Leagues.Commands.SendLeagueInvite;
using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Application.UI.Leagues.Queries.GetInviteableUsers;
using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueById;
using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueScoresByWeek;
using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekMatchups;
using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekOverview;
using SportsData.Api.Application.UI.Leagues.Queries.GetPublicLeagues;
using SportsData.Api.Application.UI.Leagues.Queries.GetUserLeagues;
using SportsData.Api.Extensions;
using SportsData.Core.Common;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Contest;

namespace SportsData.Api.Application.UI.Leagues;

[ApiController]
[Route("ui/leagues")]
public class LeagueController : ApiControllerBase
{
    [HttpPost]
    [Authorize]
    [Obsolete("Use POST /ui/leagues/football/ncaa instead. This alias will be removed once the FE cuts over.")]
    public Task<ActionResult<Guid>> Create(
        [FromBody] CreateFootballNcaaLeagueRequest request,
        [FromServices] ICreateFootballNcaaLeagueCommandHandler handler,
        CancellationToken cancellationToken)
        => CreateFootballNcaaLeague(request, handler, cancellationToken);

    [HttpPost("football/ncaa")]
    [Authorize]
    public async Task<ActionResult<Guid>> CreateFootballNcaaLeague(
        [FromBody] CreateFootballNcaaLeagueRequest request,
        [FromServices] ICreateFootballNcaaLeagueCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetCurrentUserId();

        var result = await handler.ExecuteAsync(request, userId, cancellationToken);

        if (result.IsSuccess)
            return CreatedAtAction(nameof(GetById), new { id = result.Value }, new { id = result.Value });

        return result.ToActionResult();
    }

    [HttpPost("football/nfl")]
    [Authorize]
    public async Task<ActionResult<Guid>> CreateFootballNflLeague(
        [FromBody] CreateFootballNflLeagueRequest request,
        [FromServices] ICreateFootballNflLeagueCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetCurrentUserId();

        var result = await handler.ExecuteAsync(request, userId, cancellationToken);

        if (result.IsSuccess)
            return CreatedAtAction(nameof(GetById), new { id = result.Value }, new { id = result.Value });

        return result.ToActionResult();
    }

    [HttpPost("baseball/mlb")]
    [Authorize]
    public async Task<ActionResult<Guid>> CreateBaseballMlbLeague(
        [FromBody] CreateBaseballMlbLeagueRequest request,
        [FromServices] ICreateBaseballMlbLeagueCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetCurrentUserId();

        var result = await handler.ExecuteAsync(request, userId, cancellationToken);

        if (result.IsSuccess)
            return CreatedAtAction(nameof(GetById), new { id = result.Value }, new { id = result.Value });

        return result.ToActionResult();
    }

    /// <summary>
    /// Distinct calendar dates (US Eastern) that have at least one scheduled game
    /// for the given sport/league within [from, to]. Backs the create-league
    /// date picker's blackout-date logic — the FE enables these dates and treats
    /// the rest of the range as no-game days. Either bound may be omitted.
    /// </summary>
    [HttpGet("{sport}/{league}/game-dates")]
    [Authorize]
    public async Task<ActionResult<object>> GetGameDates(
        [FromRoute] string sport,
        [FromRoute] string league,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromServices] IContestClientFactory contestClientFactory,
        CancellationToken cancellationToken)
    {
        if (!TryResolveSport(sport, league, out var resolvedSport))
            return BadRequest($"Unsupported sport/league '{sport}/{league}'.");

        var result = await contestClientFactory
            .Resolve(resolvedSport)
            .GetGameDates(from, to, cancellationToken);

        if (result.IsSuccess)
            return Ok(new { gameDates = result.Value });

        return result.ToActionResult();
    }

    // Maps the create-league route slugs (mirrors POST /ui/leagues/{sport}/{league})
    // to the Sport enum. Kept local — there's no shared slug→Sport resolver yet.
    private static bool TryResolveSport(string sport, string league, out Sport resolved)
    {
        resolved = (sport?.ToLowerInvariant(), league?.ToLowerInvariant()) switch
        {
            ("baseball", "mlb") => Sport.BaseballMlb,
            ("football", "nfl") => Sport.FootballNfl,
            ("football", "ncaa") => Sport.FootballNcaa,
            _ => Sport.All
        };
        // Sport.All (== 0) is the "unresolved" sentinel — not a valid single sport here.
        return resolved != Sport.All;
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
    public async Task<ActionResult<bool>> SendInvite(
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
            InviteeName = request.InviteeName,
            InvitedByUserId = HttpContext.GetCurrentUserId()
        };

        var result = await handler.ExecuteAsync(command, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{id}/invite/search")]
    [Authorize]
    public async Task<ActionResult<List<InviteableUserDto>>> SearchInviteableUsers(
        Guid id,
        [FromQuery] string? q,
        [FromServices] IGetInviteableUsersQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetInviteableUsersQuery
        {
            LeagueId = id,
            RequestingUserId = HttpContext.GetCurrentUserId(),
            Q = q
        };

        var result = await handler.ExecuteAsync(query, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{id}/invite/user")]
    [Authorize]
    public async Task<ActionResult<bool>> InviteUser(
        Guid id,
        [FromBody] InviteUserRequest request,
        [FromServices] IInviteUserToLeagueCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new InviteUserToLeagueCommand
        {
            LeagueId = id,
            InviteeUserId = request.UserId,
            InvitedByUserId = HttpContext.GetCurrentUserId()
        };

        var result = await handler.ExecuteAsync(command, cancellationToken);
        return result.ToActionResult();
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
