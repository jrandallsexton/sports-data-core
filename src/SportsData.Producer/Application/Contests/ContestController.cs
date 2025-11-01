using MassTransit.Initializers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Competitions;
using SportsData.Producer.Application.Contests.Overview;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Contests
{
    [Route("api/contest")]
    [ApiController]
    public class ContestController : ControllerBase
    {
        private readonly IProvideBackgroundJobs _backgroundJobProvider;
        private readonly IContestOverviewService _contestOverviewService;
        private readonly TeamSportDataContext _dataContext;

        public ContestController(
            IProvideBackgroundJobs backgroundJobProvider,
            IContestOverviewService contestOverviewService,
            TeamSportDataContext dataContext)
        {
            _backgroundJobProvider = backgroundJobProvider;
            _contestOverviewService = contestOverviewService;
            _dataContext = dataContext;
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

        [HttpPost("{id}/media/refresh")]
        public async Task<ActionResult> RefreshContestMediaById([FromRoute] Guid id)
        {
            // get the competitionId
            var competitionId = await _dataContext.Competitions
                .Where(c => c.ContestId == id)
                .FirstAsync()
                .Select(x => x.Id);

            _backgroundJobProvider.Enqueue<ICompetitionService>(p => p.RefreshCompetitionMedia(competitionId, true));
            return Accepted(id);
        }

        [HttpPost("{id}/replay")]
        public IActionResult ReplayContestById([FromRoute] Guid id)
        {
            //var cmd = new ReplayContestCommand(
            //    id,
            //    Guid.NewGuid());
            //_backgroundJobProvider.Enqueue<IReplayContests>(p => p.Process(cmd));
            return Ok(new { Message = $"Contest {id} replay initiated." });
        }
    }
}