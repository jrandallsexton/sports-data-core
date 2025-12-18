using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;

namespace SportsData.Producer.Application.FranchiseSeasonRankings
{
    [Route("api/franchise-season-ranking")]
    public class FranchiseSeasonRankingController : ControllerBase
    {
        private readonly IFranchiseSeasonRankingService _service;
        private readonly ILogger<FranchiseSeasonRankingController> _logger;

        public FranchiseSeasonRankingController(
            IFranchiseSeasonRankingService service,
            ILogger<FranchiseSeasonRankingController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        [Route("seasonYear/{seasonYear}")]
        public async Task<ActionResult<List<FranchiseSeasonPollDto>>> GetCurrentPolls([FromRoute] int seasonYear)
        {
            _logger.LogInformation(
                "FranchiseSeasonRankingController.GetCurrentPolls called with seasonYear={SeasonYear}", 
                seasonYear);
            
            try
            {
                var result = await _service.GetCurrentPolls(seasonYear);
                
                if (result.IsSuccess)
                {
                    _logger.LogInformation(
                        "GetCurrentPolls succeeded for seasonYear={SeasonYear}, returned {Count} polls", 
                        seasonYear, 
                        result.Value.Count);
                }
                else
                {
                    _logger.LogWarning(
                        "GetCurrentPolls failed for seasonYear={SeasonYear}, Status={Status}", 
                        seasonYear, 
                        result.Status);
                }
                
                return result.ToActionResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex, 
                    "Unhandled exception in GetCurrentPolls for seasonYear={SeasonYear}", 
                    seasonYear);
                throw;
            }
        }
    }
}
