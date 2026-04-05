using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;
using SportsData.Producer.Application.FranchiseSeasonRankings.Queries.GetCurrentPolls;
using SportsData.Producer.Application.FranchiseSeasonRankings.Queries.GetPollBySeasonWeekId;
using SportsData.Producer.Application.FranchiseSeasonRankings.Queries.GetRankingsByPollByWeek;

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

        [HttpGet("by-poll")]
        public async Task<ActionResult<RankingsByPollIdByWeekDto>> GetRankingsByPollByWeek(
            [FromQuery] string poll,
            [FromQuery] int seasonYear,
            [FromQuery] int weekNumber,
            [FromServices] IGetRankingsByPollByWeekQueryHandler handler,
            CancellationToken cancellationToken = default)
        {
            var query = new GetRankingsByPollByWeekQuery(poll, seasonYear, weekNumber);
            var result = await handler.ExecuteAsync(query, cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("by-week/{seasonWeekId}/poll/{pollSlug}")]
        public async Task<ActionResult<FranchiseSeasonPollDto>> GetPollBySeasonWeekId(
            [FromRoute] Guid seasonWeekId,
            [FromRoute] string pollSlug,
            [FromServices] IGetPollBySeasonWeekIdQueryHandler handler,
            CancellationToken cancellationToken = default)
        {
            var query = new GetPollBySeasonWeekIdQuery
            {
                SeasonWeekId = seasonWeekId,
                PollSlug = pollSlug
            };
            var result = await handler.ExecuteAsync(query, cancellationToken);
            return result.ToActionResult();
        }
    }
}
