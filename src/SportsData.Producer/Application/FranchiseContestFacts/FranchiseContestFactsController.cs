using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Extensions;
using SportsData.Producer.Application.FranchiseContestFacts.Commands.ProjectContestFacts;

namespace SportsData.Producer.Application.FranchiseContestFacts
{
    /// <summary>
    /// War Room team-game projection (football only: the handler depends on
    /// FootballDataContext).
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
            [FromServices] IProjectContestFactsCommandHandler handler,
            CancellationToken cancellationToken)
        {
            var result = await handler.ExecuteAsync(new ProjectContestFactsCommand(contestId), cancellationToken);
            return result.ToActionResult();
        }
    }
}
