using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SportsData.Api.Application.UI.Rankings
{
    [ApiController]
    [Route("ui/rankings")]
    [Authorize]
    public class RankingsController : ControllerBase
    {
        private readonly IRankingsService _rankingsService;
        public RankingsController(IRankingsService rankingsService)
        {
            _rankingsService = rankingsService;
        }

        [HttpGet("{seasonYear}/week/{week}")]
        public async Task<IActionResult> GetRankings(
            [FromRoute] int seasonYear,
            [FromRoute] int week,
            CancellationToken cancellationToken)
        {
            var rankings = await _rankingsService.GetRankingsByPollWeek(seasonYear, week, cancellationToken);
            return Ok(rankings);
        }
    }
}
