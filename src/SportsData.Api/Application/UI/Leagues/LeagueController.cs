using MassTransit.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Application.UI.Leagues.LeagueCreationPage.Dtos;
using SportsData.Api.Application.UI.Leagues.LeagueInvitation.Dtos;
using SportsData.Api.Extensions;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Notifications;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Leagues;

[ApiController]
[Route("ui/league")]
public class LeagueController : ApiControllerBase
{
    private readonly ILeagueService _iLeagueService;
    private readonly AppDataContext _dbContext;
    private readonly INotificationService _notificationService;
    private readonly NotificationConfig _notificationConfig;

    public LeagueController(
        ILeagueService iLeagueService,
        AppDataContext dbContext,
        INotificationService notificationService, IOptions<NotificationConfig> notificationConfig)
    {
        _iLeagueService = iLeagueService;
        _dbContext = dbContext;
        _notificationService = notificationService;
        _notificationConfig = notificationConfig.Value;
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateLeagueRequest request)
    {
        var userId = HttpContext.GetCurrentUserId();

        var leagueId = await _iLeagueService.CreateAsync(request, userId);

        return CreatedAtAction(nameof(GetById), new { id = leagueId }, new { id = leagueId });
    }
    
    [HttpGet("{id}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id)
    {
        var league = await _dbContext.PickemGroups
            .Include(x => x.Conferences)
            .Include(x => x.Members)
            .ThenInclude(m => m.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (league is null)
            return NotFound();

        var dto = new LeagueDetailDto
        {
            Id = league.Id,
            Name = league.Name,
            Description = league.Description,
            PickType = league.PickType.ToString().ToLowerInvariant(),
            UseConfidencePoints = league.UseConfidencePoints,
            TiebreakerType = league.TiebreakerType.ToString().ToLowerInvariant(),
            TiebreakerTiePolicy = league.TiebreakerTiePolicy.ToString().ToLowerInvariant(),
            RankingFilter = league.RankingFilter.ToString(),
            ConferenceSlugs = league.Conferences?.Select(c => c.ConferenceSlug).ToList() ?? new(),
            IsPublic = league.IsPublic,
            Members = league.Members.Select(m => new LeagueDetailDto.LeagueMemberDto
            {
                UserId = m.UserId,
                Username = m.User?.DisplayName ?? "UNKNOWN",
                Role = m.Role.ToString().ToLowerInvariant()
            }).ToList()
        };

        return Ok(dto);
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<List<LeagueSummaryDto>>> GetLeagues()
    {
        var userId = HttpContext.GetCurrentUserId();

        var leagues = await _dbContext.PickemGroupMembers
            .Where(m => m.UserId == userId)
            .Include(m => m.Group)
            .Select(m => new LeagueSummaryDto
            {
                Id = m.Group.Id,
                Name = m.Group.Name,
                Sport = m.Group.Sport.ToString(),
                LeagueType = m.Group.PickType.ToString(),
                UseConfidencePoints = m.Group.UseConfidencePoints,
                MemberCount = m.Group.Members.Count
            })
            .ToListAsync();

        return Ok(leagues);
    }

    [HttpPost("{id}/join")]
    [Authorize]
    public async Task<IActionResult> JoinLeague(string id)
    {
        if (!Guid.TryParse(id, out var leagueId))
            return BadRequest("Invalid league ID format.");

        var userId = HttpContext.GetCurrentUserId();

        var result = await _iLeagueService.JoinLeague(leagueId, userId);

        if (result.IsSuccess)
        {
            return Ok(new { Message = "Successfully joined the league." });
        }

        if (result is Failure<Guid?> failure)
        {
            return result.Status switch
            {
                ResultStatus.Validation => BadRequest(new { failure.Errors }),
                ResultStatus.NotFound => NotFound(new { failure.Errors }),
                ResultStatus.Unauthorized => Unauthorized(new { failure.Errors }),
                ResultStatus.Forbid => Forbid(),
                _ => StatusCode(500, new { failure.Errors })
            };
        }

        return StatusCode(500); // fallback safety
    }

    [HttpGet("{id}/matchups/{week}")]
    [Authorize]
    public async Task<ActionResult<LeagueWeekMatchupsDto>> GetMatchupsForLeagueWeek(Guid id, int week)
    {
        var userId = HttpContext.GetCurrentUserId();
        var result = await _iLeagueService.GetMatchupsForLeagueWeekAsync(userId, id, week);
        return Ok(result);
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetCurrentUserId();

        try
        {
            await _iLeagueService.DeleteLeague(userId, id, cancellationToken);
            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id}/invite")]
    [Authorize]
    public async Task<IActionResult> SendInvite(Guid id, [FromBody] SendLeagueInviteRequest request)
    {
        if (id != request.LeagueId)
            return BadRequest("Mismatched league ID in route vs body.");

        var league = await _dbContext.PickemGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (league is null)
            return NotFound();

        var userId = HttpContext.GetCurrentUserId();

        // You can enhance this later to check if the user is a league admin, etc.

        // TODO: Dynamically set the domain based on environment
        var inviteUrl = $"https://dev.sportdeets.com/app/join/{league.Id.ToString().Replace("-", string.Empty)}";

        await _notificationService.SendEmailAsync(
            request.Email,
            _notificationConfig.Email.TemplateIdInvitation ,
            new
            {
                firstName = request.InviteeName ?? "friend",
                leagueName = league.Name,
                joinUrl = inviteUrl
            });

        return Ok(new { Message = "Invite sent." });
    }

}