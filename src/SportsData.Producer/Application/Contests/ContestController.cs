using MassTransit.Initializers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Competitions;
using SportsData.Producer.Application.Competitions.Commands.RefreshCompetitionMedia;
using SportsData.Producer.Application.Contests.Overview;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Contests
{
    [Route("api/contest")]
    [ApiController]
    public class ContestController : ControllerBase
    {
        private readonly ILogger<ContestController> _logger;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;
        private readonly IContestOverviewService _contestOverviewService;
        private readonly TeamSportDataContext _dataContext;

        public ContestController(
            ILogger<ContestController> logger,
            IProvideBackgroundJobs backgroundJobProvider,
            IContestOverviewService contestOverviewService,
            TeamSportDataContext dataContext)
        {
            _logger = logger;
            _backgroundJobProvider = backgroundJobProvider;
            _contestOverviewService = contestOverviewService;
            _dataContext = dataContext;
        }

        [HttpPost]
        [Route("{contestId}/update")]
        public IActionResult UpdateContest([FromRoute] Guid contestId)
        {
            var correlationId = ActivityExtensions.GetCorrelationId();
            
            _logger.LogInformation(
                "UpdateContest requested. ContestId={ContestId}, CorrelationId={CorrelationId}",
                contestId,
                correlationId);
                
            var cmd = new UpdateContestCommand(
                contestId,
                SourceDataProvider.Espn,
                Sport.FootballNcaa,
                correlationId);
                
            _backgroundJobProvider.Enqueue<IUpdateContests>(p => p.Process(cmd));
            
            return Accepted(new { CorrelationId = correlationId, ContestId = contestId });
        }

        [HttpPost]
        [Route("{contestId}/enrich")]
        public IActionResult EnrichContest([FromRoute] Guid contestId)
        {
            var correlationId = ActivityExtensions.GetCorrelationId();
            
            _logger.LogInformation(
                "EnrichContest requested. ContestId={ContestId}, CorrelationId={CorrelationId}",
                contestId,
                correlationId);
                
            var cmd = new EnrichContestCommand(
                contestId,
                correlationId);
                
            _backgroundJobProvider.Enqueue<IEnrichContests>(p => p.Process(cmd));
            
            return Accepted(new { CorrelationId = correlationId, ContestId = contestId });
        }

        [HttpPost]
        [Route("{contestId}/stream")]
        public async Task<IActionResult> StartStream([FromRoute] Guid contestId, CancellationToken cancellationToken)
        {
            var correlationId = ActivityExtensions.GetCorrelationId();

            _logger.LogInformation(
                "StartStream requested. ContestId={ContestId}, CorrelationId={CorrelationId}",
                contestId,
                correlationId);

            var contest = await _dataContext.Contests
                .FirstOrDefaultAsync(c => c.Id == contestId, cancellationToken);

            if (contest == null)
            {
                return NotFound($"Contest {contestId} not found");
            }

            var competition = await _dataContext.Competitions
                .FirstOrDefaultAsync(c => c.ContestId == contestId, cancellationToken);

            if (competition == null)
            {
                return NotFound($"Competition for contest {contestId} not found");
            }

            var command = new StreamFootballCompetitionCommand
            {
                Sport = contest.Sport,
                SeasonYear = contest.SeasonYear,
                DataProvider = SourceDataProvider.Espn,
                ContestId = contest.Id,
                CompetitionId = competition.Id,
                CorrelationId = correlationId
            };

            _backgroundJobProvider.Enqueue<IFootballCompetitionBroadcastingJob>(job => 
                job.ExecuteAsync(command, CancellationToken.None));

            return Ok(new { Message = "Streamer started", Command = command });
        }

        [HttpGet("{id}/overview")]
        public async Task<ActionResult<ContestOverviewDto>> GetContestById([FromRoute] Guid id)
        {
            var correlationId = ActivityExtensions.GetCorrelationId();
            
            try
            {
                _logger.LogInformation(
                    "GetContestOverview requested. ContestId={ContestId}, CorrelationId={CorrelationId}",
                    id,
                    correlationId);
                    
                var contest = await _contestOverviewService.GetContestOverviewByContestId(id);
                return Ok(contest);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Contest not found. ContestId={ContestId}, CorrelationId={CorrelationId}",
                    id,
                    correlationId);
                return NotFound();
            }
        }

        [HttpPost("{id}/media/refresh")]
        public async Task<ActionResult<Guid>> RefreshContestMediaById([FromRoute] Guid id)
        {
            var correlationId = ActivityExtensions.GetCorrelationId();
            
            _logger.LogInformation(
                "RefreshContestMedia requested. ContestId={ContestId}, CorrelationId={CorrelationId}",
                id,
                correlationId);
                
            // get the competitionId
            var competitionId = await _dataContext.Competitions
                .Where(c => c.ContestId == id)
                .Select(x => x.Id)
                .FirstOrDefaultAsync();

            if (competitionId == default)
            {
                _logger.LogWarning(
                    "Competition not found for contest. ContestId={ContestId}, CorrelationId={CorrelationId}",
                    id,
                    correlationId);
                return NotFound();
            }

            _logger.LogInformation(
                "Enqueuing RefreshCompetitionMedia. ContestId={ContestId}, CompetitionId={CompetitionId}, CorrelationId={CorrelationId}",
                id,
                competitionId,
                correlationId);

            var command = new RefreshCompetitionMediaCommand(competitionId, RemoveExisting: true);
            _backgroundJobProvider.Enqueue<IRefreshCompetitionMediaCommandHandler>(
                h => h.ExecuteAsync(command, CancellationToken.None));
                
            return Accepted(correlationId);
        }

        [HttpPost("{id}/replay")]
        public IActionResult ReplayContestById([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            var correlationId = ActivityExtensions.GetCorrelationId();
            
            _logger.LogInformation(
                "ReplayContest requested. ContestId={ContestId}, CorrelationId={CorrelationId}",
                id,
                correlationId);
                
            _backgroundJobProvider.Enqueue<IContestReplayService>(
                p => p.ReplayContest(id, correlationId, cancellationToken));
                
            return Accepted(new { CorrelationId = correlationId, ContestId = id });
        }

        [HttpPost("/seasonYear/{seasonYear}/week/{seasonWeekNumber}/replay")]
        public async Task<IActionResult> ReplaySeasonWeekContests(
            [FromRoute] int seasonYear, 
            [FromRoute] int seasonWeekNumber,
            CancellationToken cancellationToken)
        {
            var correlationId = ActivityExtensions.GetCorrelationId();
            
            _logger.LogInformation(
                "ReplaySeasonWeek requested. SeasonYear={SeasonYear}, WeekNumber={WeekNumber}, CorrelationId={CorrelationId}",
                seasonYear,
                seasonWeekNumber,
                correlationId);
                
            var seasonWeekId = await _dataContext.SeasonWeeks
                .Include(sw => sw.Season)
                .Where(sw => sw.Season!.Year == seasonYear && sw.Number == seasonWeekNumber)
                .Select(sw => sw.Id)
                .FirstOrDefaultAsync(cancellationToken);

            var contestIds = await _dataContext.Contests
                .Where(c => c.SeasonYear == seasonYear && c.SeasonWeekId == seasonWeekId)
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);

            _logger.LogInformation(
                "Found {Count} contests to replay. SeasonYear={SeasonYear}, WeekNumber={WeekNumber}, CorrelationId={CorrelationId}",
                contestIds.Count,
                seasonYear,
                seasonWeekNumber,
                correlationId);

            foreach (var contestId in contestIds)
            {
                _backgroundJobProvider.Enqueue<IContestReplayService>(
                    p => p.ReplayContest(contestId, correlationId, cancellationToken));
            }

            return Accepted(new { CorrelationId = correlationId, ContestCount = contestIds.Count });
        }


    }
}