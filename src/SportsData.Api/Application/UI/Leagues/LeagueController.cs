using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Application.UI.Leagues.LeagueCreationPage.Dtos;
using SportsData.Api.Application.UI.Leagues.LeagueInvitation.Dtos;
using SportsData.Api.Extensions;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Notifications;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.UI.Leagues;

[ApiController]
[Route("ui/league")]
public class LeagueController : ApiControllerBase
{
    private readonly ILeagueService _iLeagueService;
    private readonly AppDataContext _dbContext;
    private readonly INotificationService _notificationService;
    private readonly NotificationConfig _notificationConfig;
    private readonly ILogger<LeagueController> _logger;

    public LeagueController(
        ILeagueService iLeagueService,
        AppDataContext dbContext,
        INotificationService notificationService,
        IOptions<NotificationConfig> notificationConfig,
        ILogger<LeagueController> logger)
    {
        _iLeagueService = iLeagueService;
        _dbContext = dbContext;
        _notificationService = notificationService;
        _notificationConfig = notificationConfig.Value;
        _logger = logger;
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateLeagueRequest request)
    {
        var userId = HttpContext.GetCurrentUserId();

        var result = await _iLeagueService.CreateAsync(request, userId);

        if (result.IsSuccess)
            return CreatedAtAction(nameof(GetById), new { id = result.Value }, new { id = result.Value });

        return result.ToActionResult();
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
            .AsSplitQuery()
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
    public async Task<ActionResult<Guid?>> JoinLeague([FromRoute] string id)
    {
        if (!Guid.TryParse(id, out var leagueId))
            return BadRequest("Invalid league ID format.");

        var userId = HttpContext.GetCurrentUserId();

        var result = await _iLeagueService.JoinLeague(leagueId, userId);

        return result.ToActionResult();
    }

    [HttpGet("{id}/matchups/{week}")]
    [Authorize]
    public async Task<ActionResult<LeagueWeekMatchupsDto>> GetMatchupsForLeagueWeek(
        [FromRoute]Guid id,
        [FromRoute]int week)
    {
        _logger.LogInformation(
            "GetMatchupsForLeagueWeek called with leagueId={LeagueId}, week={Week}", 
            id, 
            week);
        
        try
        {
            var userId = HttpContext.GetCurrentUserId();
            
            _logger.LogDebug(
                "Resolved userId={UserId} for GetMatchupsForLeagueWeek, leagueId={LeagueId}, week={Week}", 
                userId, 
                id, 
                week);
            
            var result = await _iLeagueService.GetMatchupsForLeagueWeekAsync(userId, id, week);
            
            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "GetMatchupsForLeagueWeek succeeded for leagueId={LeagueId}, week={Week}, userId={UserId}, returned {Count} matchups", 
                    id, 
                    week, 
                    userId, 
                    result.Value.Matchups.Count);
            }
            else
            {
                _logger.LogWarning(
                    "GetMatchupsForLeagueWeek failed for leagueId={LeagueId}, week={Week}, userId={UserId}, Status={Status}", 
                    id, 
                    week, 
                    userId, 
                    result.Status);
            }
            
            return result.ToActionResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex, 
                "Unhandled exception in GetMatchupsForLeagueWeek for leagueId={LeagueId}, week={Week}", 
                id, 
                week);
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<ActionResult<Guid>> Delete(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetCurrentUserId();

        var result = await _iLeagueService.DeleteLeague(userId, id, cancellationToken);
        
        if (result.IsSuccess)
            return NoContent();

        return result.ToActionResult();
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

    [HttpGet("discover")]
    [Authorize]
    public async Task<ActionResult<List<PublicLeagueDto>>> GetPublicLeagues()
    {
        var userId = HttpContext.GetCurrentUserId();

        var result = await _iLeagueService.GetPublicLeagues(userId);

        return result.ToActionResult();
    }

    [HttpGet("{id}/overview/{week}")]
    [Authorize]
    public async Task<ActionResult<LeagueWeekOverviewDto>> GetLeagueWeekOverview(
        [FromRoute] Guid id,
        [FromRoute] int week)
    {
        var result = await _iLeagueService.GetLeagueWeekOverview(id, week);

        return result.ToActionResult();
    }

    [HttpPost("{id}/previews/{weekId}/generate")]
    public async Task<ActionResult<Guid>> GenerateMatchupPreviews(
        [FromRoute] Guid id,
        [FromRoute] int weekId)
    {
        var result = await _iLeagueService.GenerateLeagueWeekPreviews(id, weekId);
        
        if (result.IsSuccess)
            return Accepted(new { correlationId = result.Value });

        return result.ToActionResult();
    }
}