using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;
using SportsData.Producer.Application.FranchiseSeasonRankings.Queries.GetCurrentPolls;

namespace SportsData.Producer.Application.FranchiseSeasonRankings
{
    [Route("api/franchise-season-rankings")]
    public class FranchiseSeasonRankingController : ApiControllerBase
    {
        [HttpGet("seasonYear/{seasonYear}")]
        public async Task<ActionResult<List<FranchiseSeasonPollDto>>> GetCurrentPolls(
            [FromRoute] int seasonYear,
            [FromServices] IGetCurrentPollsQueryHandler handler,
            CancellationToken cancellationToken = default)
        {
            var query = new GetCurrentPollsQuery { SeasonYear = seasonYear };
            var result = await handler.ExecuteAsync(query, cancellationToken);
            return result.ToActionResult();
        }
    }
}
