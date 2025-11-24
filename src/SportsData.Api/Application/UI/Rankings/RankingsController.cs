using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

using SportsData.Api.Application.UI.Rankings.Dtos;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.UI.Rankings
{
    [ApiController]
    [Route("ui/rankings")]
    [Authorize]
    public class RankingsController : ApiControllerBase
    {
        private readonly IRankingsService _rankingsService;
        private readonly ILogger<RankingsController> _logger;
        
        public RankingsController(
            IRankingsService rankingsService,
            ILogger<RankingsController> logger)
        {
            _rankingsService = rankingsService;
            _logger = logger;
        }

        [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]
        [OutputCache(Duration = 6000)]
        [HttpGet("{seasonYear}")]
        public async Task<ActionResult<List<RankingsByPollIdByWeekDto>>> GetPolls(
            [FromRoute] int seasonYear,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("GetPolls called with seasonYear={SeasonYear}", seasonYear);
            
            try
            {
                var result = await _rankingsService.GetRankingsBySeasonYear(
                    seasonYear,
                    cancellationToken);
                
                if (result.IsSuccess)
                {
                    _logger.LogInformation(
                        "GetPolls succeeded for seasonYear={SeasonYear}, returned {Count} polls", 
                        seasonYear, 
                        result.Value.Count);
                }
                else
                {
                    _logger.LogWarning(
                        "GetPolls failed for seasonYear={SeasonYear}, Status={Status}", 
                        seasonYear, 
                        result.Status);
                }
                
                return result.ToActionResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex, 
                    "Unhandled exception in GetPolls for seasonYear={SeasonYear}", 
                    seasonYear);
                throw;
            }
        }

        [HttpGet("{seasonYear}/week/{week}")]
        public async Task<ActionResult<List<RankingsByPollIdByWeekDto>>> GetPollsBySeasonYearWeek(
            [FromRoute] int seasonYear,
            [FromRoute] int week,
            CancellationToken cancellationToken)
        {
            var result = await _rankingsService.GetPollRankingsByPollWeek(
                seasonYear,
                week,
                cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("{seasonYear}/week/{week}/poll/{poll}")]
        public async Task<ActionResult<RankingsByPollIdByWeekDto>> GetRankings(
            [FromRoute] int seasonYear,
            [FromRoute] int week,
            [FromRoute] string poll,
            CancellationToken cancellationToken)
        {
            var result = await _rankingsService.GetRankingsByPollWeek(
                seasonYear,
                week,
                poll,
                cancellationToken);
            return result.ToActionResult();
        }
    }
}
