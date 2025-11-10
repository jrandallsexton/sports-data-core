using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;

namespace SportsData.Producer.Application.FranchiseSeasonRankings
{
    [Route("api/franchise-season-ranking")]
    public class FranchiseSeasonRankingController : ControllerBase
    {
        private readonly IFranchiseSeasonRankingService _service;

        public FranchiseSeasonRankingController(IFranchiseSeasonRankingService service)
        {
            _service = service;
        }

        [HttpGet]
        [Route("seasonYear/{seasonYear}")]
        public async Task<ActionResult<List<FranchiseSeasonPollDto>>> GetCurrentPolls([FromRoute] int seasonYear)
        {
            var result = await _service.GetCurrentPolls(seasonYear);
            return result.ToActionResult();
        }
    }
}
