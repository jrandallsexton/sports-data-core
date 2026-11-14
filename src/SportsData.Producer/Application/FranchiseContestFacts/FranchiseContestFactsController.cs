using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Extensions;
using SportsData.Producer.Application.FranchiseContestFacts.Commands.ProjectContestFacts;
using SportsData.Producer.Application.FranchiseContestFacts.Commands.RebuildSeasonFacts;

namespace SportsData.Producer.Application.FranchiseContestFacts
{
    /// <summary>
    /// War Room team-game projection (football only: the handler depends on
    /// FootballDataContext and is registered only on football pods).
    /// </summary>
    [Route("api/franchise-contest-facts")]
    [ApiController]
    public class FranchiseContestFactsController : ControllerBase
    {
        /// <summary>
        /// (Re)project one contest's two team rows. Idempotent; a contest that
        /// is not projectable (not finalized, cancelled, out of scope) has its
        /// rows removed. Returns the row count the contest now has.
        /// </summary>
        [HttpPost("contests/{contestId:guid}")]
        public async Task<ActionResult<int>> ProjectContest(
            [FromRoute] Guid contestId,
            [FromServices] IAppMode appMode,
            CancellationToken cancellationToken)
        {
            // Resolved after the mode guard (same as CompetitionController's
            // drives/metrics endpoints) so a non-football pod returns a typed
            // BadRequest instead of a DI resolution failure.
            if (appMode.CurrentSport is not (Sport.FootballNcaa or Sport.FootballNfl))
                return BadRequest($"FranchiseContestFacts is football-only. CurrentSport={appMode.CurrentSport}");

            var handler = HttpContext.RequestServices.GetRequiredService<IProjectContestFactsCommandHandler>();
            var result = await handler.ExecuteAsync(new ProjectContestFactsCommand(contestId), cancellationToken);
            return result.ToActionResult();
        }

        /// <summary>
        /// Enqueue a re-projection of every contest in the season (backfill,
        /// or after a ProjectionVersion bump). Returns the number enqueued.
        /// </summary>
        [HttpPost("seasons/{seasonYear:int}")]
        public async Task<ActionResult<int>> RebuildSeason(
            [FromRoute] int seasonYear,
            [FromServices] IAppMode appMode,
            CancellationToken cancellationToken)
        {
            if (appMode.CurrentSport is not (Sport.FootballNcaa or Sport.FootballNfl))
                return BadRequest($"FranchiseContestFacts is football-only. CurrentSport={appMode.CurrentSport}");

            var handler = HttpContext.RequestServices.GetRequiredService<IRebuildSeasonFactsCommandHandler>();
            var result = await handler.ExecuteAsync(new RebuildSeasonFactsCommand(seasonYear), cancellationToken);
            return result.ToActionResult();
        }
    }
}
