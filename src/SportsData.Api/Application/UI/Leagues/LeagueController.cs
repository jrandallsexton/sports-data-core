using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Application.UI.Leagues.LeagueCreationPage;
using SportsData.Api.Application.UI.Leagues.LeagueCreationPage.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Leagues;

[ApiController]
[Route("ui/league")]
public class LeagueController : ApiControllerBase
{
    private readonly ILeagueCreationService _leagueCreationService;
    private readonly AppDataContext _dbContext;

    public LeagueController(
        ILeagueCreationService leagueCreationService,
        AppDataContext dbContext)
    {
        _leagueCreationService = leagueCreationService;
        _dbContext = dbContext;
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] CreateLeagueRequest request)
    {
        var userId = new Guid("11111111-1111-1111-1111-111111111111"); // TEMPORARY HARDCODED FOR TESTING
        //var firebaseUid = User.FindFirst("user_id")?.Value;
        //if (firebaseUid == null)
        //    return Unauthorized();

        //var user = await _dbContext.Users
        //    .FirstOrDefaultAsync(u => u.FirebaseUid == firebaseUid);

        //if (user == null)
        //    return Unauthorized("User not found.");

        //// Retrieve the current authenticated user ID (from Firebase or JWT)
        //var userId = user.Id; // Extension method for ClaimTypes.NameIdentifier → Guid

        //if (userId == Guid.Empty)
        //    return Unauthorized();

        var leagueId = await _leagueCreationService.CreateAsync(request, userId);

        return CreatedAtAction(nameof(GetById), new { id = leagueId }, new { id = leagueId });
    }

    [HttpGet("{id}")]
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
}