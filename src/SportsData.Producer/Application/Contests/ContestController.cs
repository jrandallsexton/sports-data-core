
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;
using Dtos = SportsData.Core.Dtos;
using SportsData.Core.Infrastructure.Clients.Contest.Queries;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Competitions;
using SportsData.Producer.Application.Competitions.Commands.RefreshCompetitionMedia;
using SportsData.Producer.Application.Contests.Commands;
using SportsData.Producer.Application.Contests.Queries.GetContestById;
using SportsData.Producer.Application.Contests.Queries.GetContestOverview;
using SportsData.Producer.Application.Contests.Queries.GetContestPlayLog;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Contests
{
    [Route("api/contests")]
    [ApiController]
    public class ContestController : ControllerBase
    {
        private readonly ILogger<ContestController> _logger;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;
        private readonly TeamSportDataContext _dataContext;
        private readonly IAppMode _appMode;

        public ContestController(
            ILogger<ContestController> logger,
            IProvideBackgroundJobs backgroundJobProvider,
            TeamSportDataContext dataContext,
            IAppMode appMode)
        {
            _logger = logger;
            _backgroundJobProvider = backgroundJobProvider;
            _dataContext = dataContext;
            _appMode = appMode;
        }

        /// <summary>
        /// Resolves the correlation id for an inbound request. Prefers the
        /// explicit <c>X-Correlation-Id</c> HTTP header from the calling
        /// service so the same id is logged across API → Producer → Provider.
        /// Falls back to deriving from <c>Activity.Current</c> (TraceId) when
        /// the header is absent — covers direct-curl testing and any caller
        /// that doesn't propagate the header.
        /// <see cref="SportsData.Core.Infrastructure.Clients.ClientBase"/>
        /// stamps this header on every outbound POST.
        /// </summary>
        private Guid GetCorrelationIdFromRequest()
        {
            return Request.Headers.TryGetValue("X-Correlation-Id", out var headerValue)
                && Guid.TryParse(headerValue, out var inbound)
                    ? inbound
                    : ActivityExtensions.GetCorrelationId();
        }

        [HttpGet("{contestId}")]
        public async Task<ActionResult<SeasonContestDto>> GetContestById(
            [FromServices] IGetContestByIdQueryHandler handler,
            [FromRoute] Guid contestId,
            CancellationToken cancellationToken = default)
        {
            var query = new GetContestByIdQuery(contestId);
            var result = await handler.ExecuteAsync(query, cancellationToken);

            return result.ToActionResult();
        }

        [HttpPost]
        [Route("{contestId}/update")]
        public IActionResult UpdateContest([FromRoute] Guid contestId)
        {
            var correlationId = GetCorrelationIdFromRequest();

            _logger.LogInformation(
                "UpdateContest requested. ContestId={ContestId}, CorrelationId={CorrelationId}",
                contestId,
                correlationId);

            var cmd = new UpdateContestCommand(
                contestId,
                SourceDataProvider.Espn,
                _appMode.CurrentSport,
                correlationId);
                
            _backgroundJobProvider.Enqueue<IUpdateContests>(p => p.Process(cmd));
            
            return Accepted(new { CorrelationId = correlationId, ContestId = contestId });
        }

        [HttpPost]
        [Route("{contestId}/enrich")]
        public IActionResult EnrichContest([FromRoute] Guid contestId)
        {
            var correlationId = GetCorrelationIdFromRequest();

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

        /// <summary>
        /// Admin "re-run enrichment" path. Clears the derived/enriched fields
        /// on the Contest row (FinalizedUtc, WinnerFranchiseSeasonId,
        /// SpreadWinnerFranchiseSeasonId, OverUnder, AwayScore, HomeScore,
        /// AuditedUtc) and re-invokes the sport-specific enrichment processor
        /// SYNCHRONOUSLY in this request. Caller (the API admin endpoint) does
        /// not need to poll Hangfire or follow a separate cron — once this
        /// returns 200, enrichment has rerun, ContestFinalized has fired, and
        /// the API's ContestFinalizedHandler has enqueued ScorePicksCommand.
        ///
        /// This is NOT a re-source. CompetitionCompetitorScores are not
        /// touched. Enrichment reads MAX(CCS) and derives the result fields;
        /// the bug class this endpoint is for is "derived fields are wrong
        /// but the canonical scores are correct" — primarily a manual recovery
        /// path for stuck WinnerFranchiseSeasonId / SpreadWinnerFranchiseSeasonId
        /// values that the audit job hasn't caught yet.
        /// </summary>
        [HttpPost]
        [Route("{contestId}/admin/reenrich")]
        public async Task<ActionResult<ReenrichContestResponse>> ReenrichContest(
            [FromRoute] Guid contestId,
            [FromServices] IReenrichContestHandler handler,
            CancellationToken cancellationToken)
        {
            var result = await handler.ExecuteAsync(
                new ReenrichContestCommand
                {
                    ContestId = contestId,
                    CorrelationId = GetCorrelationIdFromRequest()
                },
                cancellationToken);

            return result.ToActionResult();
        }

        [HttpPost]
        [Route("finalize")]
        public IActionResult FinalizeContestsBySeasonYear(
            [FromBody] FinalizeContestsBySeasonYearCommand command)
        {
            // Body's CorrelationId wins when explicitly set (caller may want
            // to thread through a known id from a saga); otherwise fall back
            // to the request-level helper (header-or-Activity).
            var correlationId = command.CorrelationId == Guid.Empty
                ? GetCorrelationIdFromRequest()
                : command.CorrelationId;

            _logger.LogInformation(
                "FinalizeContestsBySeasonYear requested. Sport={Sport}, SeasonYear={SeasonYear}, ReprocessEnriched={ReprocessEnriched}, CorrelationId={CorrelationId}",
                command.Sport,
                command.SeasonYear,
                command.ReprocessEnriched,
                correlationId);

            var cmd = command with { CorrelationId = correlationId };

            _backgroundJobProvider.Enqueue<IFinalizeContestsBySeasonYearHandler>(h => h.ExecuteAsync(cmd, CancellationToken.None));

            return Accepted(new { CorrelationId = correlationId, Sport = command.Sport, SeasonYear = command.SeasonYear });
        }

        [HttpPost]
        [Route("{contestId}/stream")]
        public async Task<IActionResult> StartStream([FromRoute] Guid contestId, CancellationToken cancellationToken)
        {
            var correlationId = GetCorrelationIdFromRequest();

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

            var command = new StreamCompetitionCommand
            {
                Sport = contest.Sport,
                SeasonYear = contest.SeasonYear,
                DataProvider = SourceDataProvider.Espn,
                ContestId = contest.Id,
                CompetitionId = competition.Id,
                CorrelationId = correlationId
            };

            _backgroundJobProvider.Enqueue<ICompetitionBroadcastingJob>(job =>
                job.ExecuteAsync(command, CancellationToken.None));

            return Accepted(new { Message = "Streamer started", Command = command });
        }

        [HttpGet("{id}/overview")]
        public async Task<ActionResult<ContestOverviewDto>> GetContestOverview(
            [FromServices] IGetContestOverviewQueryHandler handler,
            [FromRoute] Guid id,
            CancellationToken cancellationToken = default)
        {
            var query = new GetContestOverviewQuery(id);
            var result = await handler.ExecuteAsync(query, cancellationToken);

            return result.ToActionResult();
        }

        /// <summary>
        /// Full play-by-play log for a contest. The overview endpoint above
        /// trims plays to key/scoring only to keep the typical "Contest
        /// Overview" page payload manageable; this endpoint backs the
        /// on-demand "Show all plays" expansion in the UI (e.g., 500+ MLB
        /// plays).
        /// </summary>
        [HttpGet("{id}/playlog")]
        public async Task<ActionResult<PlayLogDto>> GetContestPlayLog(
            [FromServices] IGetContestPlayLogQueryHandler handler,
            [FromRoute] Guid id,
            CancellationToken cancellationToken = default)
        {
            var query = new GetContestPlayLogQuery(id);
            var result = await handler.ExecuteAsync(query, cancellationToken);

            return result.ToActionResult();
        }

        [HttpPost("{id}/media/refresh")]
        public async Task<ActionResult<Guid>> RefreshContestMediaById([FromRoute] Guid id)
        {
            var correlationId = GetCorrelationIdFromRequest();
            
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
            var correlationId = GetCorrelationIdFromRequest();

            _logger.LogInformation(
                "ReplayContest requested. ContestId={ContestId}, Sport={Sport}, CorrelationId={CorrelationId}",
                id,
                _appMode.CurrentSport,
                correlationId);

            // Replay services are sport-specific (sport-keyed DataContext);
            // resolve via the running pod's configured sport. Hangfire
            // jobs run after the HTTP request completes, so we pass
            // CancellationToken.None — the request token would already
            // be tripped by the time the worker picks the job up.
            switch (_appMode.CurrentSport)
            {
                case Sport.FootballNcaa:
                case Sport.FootballNfl:
                    _backgroundJobProvider.Enqueue<IFootballContestReplayService>(
                        p => p.ReplayContest(id, correlationId, CancellationToken.None));
                    break;
                case Sport.BaseballMlb:
                    _backgroundJobProvider.Enqueue<IBaseballContestReplayService>(
                        p => p.ReplayContest(id, correlationId, CancellationToken.None));
                    break;
                default:
                    return BadRequest($"Replay not supported for sport '{_appMode.CurrentSport}'.");
            }

            return Accepted(new { CorrelationId = correlationId, ContestId = id });
        }

        [HttpPost("/seasonYear/{seasonYear}/week/{seasonWeekNumber}/replay")]
        public async Task<IActionResult> ReplaySeasonWeekContests(
            [FromRoute] int seasonYear, 
            [FromRoute] int seasonWeekNumber,
            CancellationToken cancellationToken)
        {
            var correlationId = GetCorrelationIdFromRequest();

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

            // Replay services are sport-specific (sport-keyed DataContext);
            // resolve via the running pod's configured sport. Hangfire
            // jobs run after the HTTP request completes, so we pass
            // CancellationToken.None — the request token would already
            // be tripped by the time the worker picks the job up.
            switch (_appMode.CurrentSport)
            {
                case Sport.FootballNcaa:
                case Sport.FootballNfl:
                    foreach (var contestId in contestIds)
                    {
                        _backgroundJobProvider.Enqueue<IFootballContestReplayService>(
                            p => p.ReplayContest(contestId, correlationId, CancellationToken.None));
                    }
                    break;
                case Sport.BaseballMlb:
                    foreach (var contestId in contestIds)
                    {
                        _backgroundJobProvider.Enqueue<IBaseballContestReplayService>(
                            p => p.ReplayContest(contestId, correlationId, CancellationToken.None));
                    }
                    break;
                default:
                    return BadRequest($"Replay not supported for sport '{_appMode.CurrentSport}'.");
            }

            return Accepted(new { CorrelationId = correlationId, ContestCount = contestIds.Count });
        }

        // ========== Matchup query endpoints (Phase 2 of CanonicalDataProvider elimination) ==========

        [HttpGet("matchups/current-week")]
        public async Task<ActionResult<List<Dtos.Canonical.Matchup>>> GetMatchupsForCurrentWeek(
            [FromServices] Queries.Matchups.GetMatchupsForCurrentWeek.IGetMatchupsForCurrentWeekQueryHandler handler,
            CancellationToken cancellationToken = default)
        {
            var result = await handler.ExecuteAsync(new Queries.Matchups.GetMatchupsForCurrentWeek.GetMatchupsForCurrentWeekQuery(), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("matchups/by-season-week")]
        public async Task<ActionResult<List<Dtos.Canonical.Matchup>>> GetMatchupsForSeasonWeek(
            [FromQuery] int year,
            [FromQuery] int week,
            [FromServices] Queries.Matchups.GetMatchupsForSeasonWeek.IGetMatchupsForSeasonWeekQueryHandler handler,
            CancellationToken cancellationToken = default)
        {
            var result = await handler.ExecuteAsync(new Queries.Matchups.GetMatchupsForSeasonWeek.GetMatchupsForSeasonWeekQuery(year, week), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("{contestId}/matchup")]
        public async Task<ActionResult<Dtos.Canonical.Matchup>> GetMatchupByContestId(
            [FromRoute] Guid contestId,
            [FromServices] Queries.Matchups.GetMatchupByContestId.IGetMatchupByContestIdQueryHandler handler,
            CancellationToken cancellationToken = default)
        {
            var result = await handler.ExecuteAsync(new Queries.Matchups.GetMatchupByContestId.GetMatchupByContestIdQuery(contestId), cancellationToken);
            return result.ToActionResult();
        }

        [HttpPost("matchups/by-ids")]
        public async Task<ActionResult<List<LeagueMatchupDto>>> GetMatchupsByContestIds(
            [FromBody] GetMatchupsByContestIdsRequest request,
            [FromServices] Queries.Matchups.GetMatchupsByContestIds.IGetMatchupsByContestIdsQueryHandler handler,
            CancellationToken cancellationToken = default)
        {
            var result = await handler.ExecuteAsync(
                new Queries.Matchups.GetMatchupsByContestIds.GetMatchupsByContestIdsQuery(
                    request.ContestIds,
                    request.Direction),
                cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("{contestId}/matchup-preview")]
        public async Task<ActionResult<MatchupForPreviewDto>> GetMatchupForPreview(
            [FromRoute] Guid contestId,
            [FromServices] Queries.Matchups.GetMatchupForPreview.IGetMatchupForPreviewQueryHandler handler,
            CancellationToken cancellationToken = default)
        {
            var result = await handler.ExecuteAsync(new Queries.Matchups.GetMatchupForPreview.GetMatchupForPreviewQuery(contestId), cancellationToken);
            return result.ToActionResult();
        }

        [HttpPost("matchups/previews")]
        public async Task<ActionResult<Dictionary<Guid, MatchupForPreviewDto>>> GetMatchupsForPreviewBatch(
            [FromBody] Guid[] contestIds,
            [FromServices] Queries.Matchups.GetMatchupForPreview.IGetMatchupForPreviewQueryHandler handler,
            CancellationToken cancellationToken = default)
        {
            var result = await handler.ExecuteBatchAsync(new Queries.Matchups.GetMatchupForPreview.GetMatchupsForPreviewBatchQuery(contestIds), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("{contestId}/result")]
        public async Task<ActionResult<MatchupResult>> GetMatchupResult(
            [FromRoute] Guid contestId,
            [FromServices] Queries.Matchups.GetMatchupResult.IGetMatchupResultQueryHandler handler,
            CancellationToken cancellationToken = default)
        {
            var result = await handler.ExecuteAsync(new Queries.Matchups.GetMatchupResult.GetMatchupResultQuery(contestId), cancellationToken);
            return result.ToActionResult();
        }

        [HttpPost("results/by-ids")]
        public async Task<ActionResult<List<ContestResultDto>>> GetContestResultsByContestIds(
            [FromBody] Guid[] contestIds,
            [FromServices] Queries.Matchups.GetContestResults.IGetContestResultsByContestIdsQueryHandler handler,
            CancellationToken cancellationToken = default)
        {
            var result = await handler.ExecuteAsync(new Queries.Matchups.GetContestResults.GetContestResultsByContestIdsQuery(contestIds), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("finalized")]
        public async Task<ActionResult<List<Guid>>> GetFinalizedContestIds(
            [FromQuery] Guid seasonWeekId,
            [FromServices] Queries.Matchups.GetFinalizedContestIds.IGetFinalizedContestIdsQueryHandler handler,
            CancellationToken cancellationToken = default)
        {
            var result = await handler.ExecuteAsync(new Queries.Matchups.GetFinalizedContestIds.GetFinalizedContestIdsQuery(seasonWeekId), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("completed-fbs")]
        public async Task<ActionResult<List<Guid>>> GetCompletedFbsContestIds(
            [FromQuery] Guid seasonWeekId,
            [FromServices] Queries.Matchups.GetCompletedFbsContestIds.IGetCompletedFbsContestIdsQueryHandler handler,
            CancellationToken cancellationToken = default)
        {
            var result = await handler.ExecuteAsync(new Queries.Matchups.GetCompletedFbsContestIds.GetCompletedFbsContestIdsQuery(seasonWeekId), cancellationToken);
            return result.ToActionResult();
        }
    }
}