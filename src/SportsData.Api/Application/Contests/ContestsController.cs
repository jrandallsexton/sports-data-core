using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.Admin;
using SportsData.Api.Application.Contests.Commands.GenerateGameRecap;
using SportsData.Api.Application.Contests.Commands.ReenrichContest;
using SportsData.Api.Application.Contests.Queries.GetContestById;
using SportsData.Api.Application.Contests.Queries.GetContestById.Dtos;
using SportsData.Api.Application.Contests.Queries.GetContestHistory;
using SportsData.Api.Application.Contests.Queries.GetMatchupForContest;
using SportsData.Api.Application.Scoring.Jobs.PickScoring;
using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Core.Common.Mapping;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Contest;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.Contests;

[Route("api/{sport}/{league}/contests")]
[ApiController]
public class ContestsController : ControllerBase
{
    private readonly IProvideBackgroundJobs _backgroundJobProvider;
    private readonly ILogger<ContestsController> _logger;

    public ContestsController(
        IProvideBackgroundJobs backgroundJobProvider,
        ILogger<ContestsController> logger)
    {
        _backgroundJobProvider = backgroundJobProvider;
        _logger = logger;
    }

    [HttpGet("{contestId:guid}")]
    [ProducesResponseType(typeof(ContestDetailResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ContestDetailResponseDto>> GetContestById(
        [FromServices] IGetContestByIdQueryHandler handler,
        [FromRoute] string sport,
        [FromRoute] string league,
        [FromRoute] Guid contestId,
        CancellationToken cancellationToken = default)
    {
        var query = new GetContestByIdQuery(sport, league, contestId);
        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }

    /// <summary>
    /// Historical context for a matchup: last N head-to-head meetings and
    /// each team's late-prior-season form — the same blocks the
    /// preview/insight models consume.
    /// </summary>
    [HttpGet("{contestId:guid}/history")]
    [ProducesResponseType(typeof(ContestPreviewHistoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContestPreviewHistoryDto>> GetContestHistory(
        [FromServices] IGetContestHistoryQueryHandler handler,
        [FromRoute] string sport,
        [FromRoute] string league,
        [FromRoute] Guid contestId,
        CancellationToken cancellationToken = default)
    {
        var query = new GetContestHistoryQuery(sport, league, contestId);
        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }

    // ─── Admin actions ───────────────────────────────────────────────
    // Moved from AdminController. This controller's GETs above are public;
    // everything below is operator tooling, so [AdminApiToken] sits on each
    // ACTION, never the class. The route snapshot test pins which is which.

    /// <summary>
    /// Re-scores every pick on a contest. Scoring is keyed by contest id
    /// alone; {sport}/{league} only places the route under the contest
    /// resource.
    /// </summary>
    [AdminApiToken]
    [HttpPost("{contestId:guid}/score")]
    public IActionResult ScoreContest([FromRoute] Guid contestId)
    {
        var cmd = new ScorePicksCommand(contestId);
        _backgroundJobProvider.Enqueue<IScorePicks>(p => p.Process(cmd));
        return Accepted(new { cmd.CorrelationId });
    }

    /// <summary>
    /// Admin re-enrichment path. Clears UserPick scoring fields for this
    /// contest, then asks Producer to clear the derived/enriched fields on
    /// its Contest row and re-invoke the enrichment processor synchronously.
    /// Returns the CorrelationId Producer logged the work under so the
    /// admin UI can surface it for Seq tracing.
    ///
    /// Manual recovery path for stuck WinnerFranchiseSeasonId /
    /// SpreadWinnerFranchiseSeasonId — the case where the canonical
    /// CompetitionCompetitorScores are correct but the derived fields are
    /// wrong and the nightly audit hasn't reset them yet.
    ///
    /// Admin-only on purpose: the operation rolls back UserPicks, so a
    /// logged-in non-admin must not be able to fire it (the UI's isAdmin
    /// check is presentational only — not a trust boundary).
    /// </summary>
    [AdminApiToken]
    [HttpPost("{contestId:guid}/reenrich")]
    public async Task<ActionResult<Guid>> ReenrichContest(
        [FromRoute] string sport,
        [FromRoute] string league,
        [FromRoute] Guid contestId,
        [FromServices] IReenrichContestCommandHandler handler,
        CancellationToken cancellationToken = default)
    {
        var mode = ModeMapper.ResolveMode(sport, league);
        var command = new ReenrichContestCommand { ContestId = contestId, Sport = mode };
        var result = await handler.ExecuteAsync(command, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Admin "re-source every contest for a (sport, season)". Fans out the
    /// narrowed Contest Refresh per contest through the matching per-sport
    /// Producer to backfill point-in-time records / play data (no athletes).
    /// Producer enqueues the work and returns 202.
    ///
    /// This is the front door for the by-season driver: Producer's own
    /// <c>/api/contests/refresh</c> endpoint is not internet-facing, so
    /// operator access routes through here and over to the correct Producer
    /// pod via the contest client factory. See
    /// docs/features/season-contest-resource-driver.md.
    ///
    /// Example: POST /api/baseball/mlb/contests/refresh?seasonYear=2026
    /// </summary>
    [AdminApiToken]
    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshContestsBySeasonYear(
        [FromRoute] string sport,
        [FromRoute] string league,
        [FromQuery] int seasonYear,
        [FromServices] IContestClientFactory contestClientFactory,
        CancellationToken cancellationToken = default)
    {
        // API runs in Sport.All; sport+league → a concrete Sport so the
        // client factory routes to the matching per-sport Producer pod.
        var mode = ModeMapper.ResolveMode(sport, league);
        var correlationId = ActivityExtensions.GetCorrelationId();

        _logger.LogInformation(
            "RefreshContestsBySeasonYear requested. Sport={Sport}, SeasonYear={SeasonYear}, CorrelationId={CorrelationId}",
            mode, seasonYear, correlationId);

        var result = await contestClientFactory
            .Resolve(mode)
            .RefreshContestsBySeasonYear(mode, seasonYear, correlationId, cancellationToken);

        return result.ToActionResult(_ =>
            Accepted(new { correlationId, sport = mode.ToString(), seasonYear }));
    }

    /// <summary>
    /// Returns one canonical matchup in the same shape as the picks page
    /// (<see cref="LeagueWeekMatchupsDto.MatchupForPickDto"/>), without league
    /// context. Backs the SignalR debug pages so a real MatchupCard can be
    /// rendered against a chosen contest. League-context fields (Predictions,
    /// AiWinner, IsPreview*, HeadLine) are intentionally null/empty.
    /// Formerly separate baseball and football actions; the sport-scoped
    /// route resolves the sport through ModeMapper for both.
    /// </summary>
    [AdminApiToken]
    [HttpGet("{contestId:guid}/matchup")]
    public async Task<ActionResult<LeagueWeekMatchupsDto.MatchupForPickDto>> GetMatchupForContest(
        [FromRoute] string sport,
        [FromRoute] string league,
        [FromRoute] Guid contestId,
        [FromServices] IGetMatchupForContestQueryHandler handler,
        CancellationToken cancellationToken = default)
    {
        var mode = ModeMapper.ResolveMode(sport, league);

        var query = new GetMatchupForContestQuery(contestId, mode);
        var result = await handler.ExecuteAsync(query, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Triggers a contest replay through the matching sport's Producer.
    /// Producer enqueues the replay and the bus emits ContestStatusChanged
    /// once + a sport-specific *PlayCompleted per stored play, exercising
    /// the same SignalR fan-out path as a live game. Use the admin baseball /
    /// football debug pages to observe the resulting events on a real
    /// MatchupCard. Formerly separate baseball and football actions.
    /// </summary>
    [AdminApiToken]
    [HttpPost("{contestId:guid}/replay")]
    public async Task<ActionResult<bool>> ReplayContest(
        [FromRoute] string sport,
        [FromRoute] string league,
        [FromRoute] Guid contestId,
        [FromServices] IContestClientFactory contestClientFactory,
        CancellationToken cancellationToken = default)
    {
        // API runs in Sport.All; the route's sport+league resolve the
        // concrete Sport that routes the client factory to the right
        // Producer pod (football's two leagues share one pod).
        var mode = ModeMapper.ResolveMode(sport, league);

        var result = await contestClientFactory
            .Resolve(mode)
            .ReplayContest(contestId, cancellationToken);

        return result.ToActionResult();
    }

    /// <summary>
    /// Generates a game recap from caller-supplied game data: the recap prompt
    /// (blob storage, cached; <c>reloadPrompt</c> forces a reload) plus the
    /// body's <c>gameDataJson</c>, sent to the AI client. An operator test
    /// harness for the same handler the scheduled ContestRecap job uses. The
    /// handler takes no sport or contest id; {sport}/{league} only places the
    /// route under the contest resource.
    ///
    /// Example: POST /api/football/ncaa/contests/recap
    /// Body: { "gameDataJson": "{ ... }", "reloadPrompt": false }
    /// </summary>
    [AdminApiToken]
    [HttpPost("recap")]
    public async Task<ActionResult<GameRecapResponse>> GenerateGameRecap(
        [FromBody] GenerateGameRecapCommand command,
        [FromServices] IGenerateGameRecapCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(command, cancellationToken);
        return result.ToActionResult();
    }
}
