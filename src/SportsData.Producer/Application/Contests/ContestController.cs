using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Contests.Overview;

namespace SportsData.Producer.Application.Contests
{
    [Route("api/contest")]
    [ApiController]
    public class ContestController : ControllerBase
    {
        private readonly IProvideBackgroundJobs _backgroundJobProvider;
        private readonly IContestOverviewService _contestOverviewService;   

        public ContestController(
            IProvideBackgroundJobs backgroundJobProvider,
            IContestOverviewService contestOverviewService)
        {
            _backgroundJobProvider = backgroundJobProvider;
            _contestOverviewService = contestOverviewService;
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

        [HttpPost]
        [Route("{contestId}/enrich")]
        public IActionResult EnrichContest([FromRoute] Guid contestId)
        {
            var cmd = new EnrichContestCommand(
                contestId,
                Guid.NewGuid());
            _backgroundJobProvider.Enqueue<IEnrichContests>(p => p.Process(cmd));
            return Ok(new { Message = $"Contest {contestId} enrichment initiated." });
        }

        [HttpGet("{id}/overview")]
        public async Task<ActionResult<ContestOverviewDto>> GetContestById([FromRoute] Guid id)
        {
            try
            {
                var contest = await _contestOverviewService.GetContestOverviewByContestId(id);
                return Ok(contest);
            }
            catch (ArgumentException)
            {
                return NotFound();
            }
        }
    }
}