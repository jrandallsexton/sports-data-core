using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.UI.PlayerLineups.Commands.ClearLineupSlot;
using SportsData.Api.Application.UI.PlayerLineups.Commands.UpsertLineupSlot;
using SportsData.Api.Application.UI.PlayerLineups.Dtos;
using SportsData.Api.Application.UI.PlayerLineups.Queries.GetMyPlayerLineup;
using SportsData.Api.Application.UI.PlayerLineups.Queries.GetPlayerStandings;
using SportsData.Api.Extensions;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.UI.PlayerLineups;

/// <summary>
/// Player Pick'em roster persistence — one lineup per user per
/// league-week, per-player derived locking (kickoff−5, the product-wide
/// rule). All routes gate on GroupType == PlayerPickem + league
/// membership inside the handlers.
/// See docs/features/player-pickem/roster-persistence.md.
/// </summary>
[ApiController]
[Route("ui/leagues/{leagueId:guid}/player-lineups")]
public class PlayerLineupsController : ApiControllerBase
{
    /// <summary>
    /// The caller's lineup for the week, slots carrying derived isLocked.
    /// First read of a new week performs the lazy carry-over clone from
    /// the most recent populated week.
    /// </summary>
    [Authorize]
    [HttpGet("{seasonYear:int}/{seasonWeek:int}/mine")]
    public async Task<ActionResult<PlayerLineupDto>> GetMine(
        [FromRoute] Guid leagueId,
        [FromRoute] int seasonYear,
        [FromRoute] int seasonWeek,
        [FromServices] IGetMyPlayerLineupQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetCurrentUserId();
        var result = await handler.ExecuteAsync(
            new GetMyPlayerLineupQuery(leagueId, userId, seasonYear, seasonWeek),
            cancellationToken);
        return result.ToActionResult();
    }

    public class UpsertSlotRequest
    {
        public Guid AthleteId { get; set; }
        public Guid AthleteSeasonId { get; set; }
        public string Position { get; set; } = null!;
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string TeamName { get; set; } = null!;
        public string TeamSlug { get; set; } = null!;
        public string? OpponentName { get; set; }
    }

    [Authorize]
    [HttpPut("{seasonYear:int}/{seasonWeek:int}/mine/slots/{slotId}")]
    public async Task<ActionResult<PlayerLineupSlotDto>> UpsertSlot(
        [FromRoute] Guid leagueId,
        [FromRoute] int seasonYear,
        [FromRoute] int seasonWeek,
        [FromRoute] string slotId,
        [FromBody] UpsertSlotRequest request,
        [FromServices] IUpsertLineupSlotCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new UpsertLineupSlotCommand
        {
            LeagueId = leagueId,
            UserId = HttpContext.GetCurrentUserId(),
            SeasonYear = seasonYear,
            SeasonWeek = seasonWeek,
            SlotId = slotId,
            AthleteId = request.AthleteId,
            AthleteSeasonId = request.AthleteSeasonId,
            Position = request.Position,
            FirstName = request.FirstName,
            LastName = request.LastName,
            TeamName = request.TeamName,
            TeamSlug = request.TeamSlug,
            OpponentName = request.OpponentName,
        };

        var result = await handler.ExecuteAsync(command, cancellationToken);
        return result.ToActionResult();
    }

    [Authorize]
    [HttpDelete("{seasonYear:int}/{seasonWeek:int}/mine/slots/{slotId}")]
    public async Task<ActionResult<bool>> ClearSlot(
        [FromRoute] Guid leagueId,
        [FromRoute] int seasonYear,
        [FromRoute] int seasonWeek,
        [FromRoute] string slotId,
        [FromServices] IClearLineupSlotCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(
            new ClearLineupSlotCommand(leagueId, HttpContext.GetCurrentUserId(), seasonYear, seasonWeek, slotId),
            cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Cumulative-points standings with weekly winners — reads persisted
    /// lineup totals only (the scoring consumers keep them fresh).
    /// </summary>
    [HttpGet("{seasonYear:int}/standings")]
    public async Task<ActionResult<PlayerStandingsDto>> GetStandings(
        [FromRoute] Guid leagueId,
        [FromRoute] int seasonYear,
        [FromServices] IGetPlayerStandingsQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetCurrentUserId();
        var result = await handler.ExecuteAsync(
            new GetPlayerStandingsQuery(leagueId, userId, seasonYear), cancellationToken);
        return result.ToActionResult();
    }

}
