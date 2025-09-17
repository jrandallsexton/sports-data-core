using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Common;
using SportsData.Core.Processing;

namespace SportsData.Producer.Application.Contests
{
    [Route("api/contest")]
    [ApiController]
    public class ContestController : ControllerBase
    {

        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public ContestController(IProvideBackgroundJobs backgroundJobProvider)
        {
            _backgroundJobProvider = backgroundJobProvider;
        }

        [HttpPost]
        [Route("{contestId}/update")]
        public IActionResult UpdateContest([FromRoute] Guid contestId)
        {
            var cmd = new UpdateContestCommand(
                contestId,
                2025,
                SourceDataProvider.Espn,
                Sport.FootballNcaa,
                Guid.NewGuid());
            _backgroundJobProvider.Enqueue<IUpdateContests>(p => p.Process(cmd));
            return Ok(new { Message = $"Contest {contestId} update initiated." });
        }
    }
}