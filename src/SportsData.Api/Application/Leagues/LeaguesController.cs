using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.Leagues.Commands.BackfillLeagueScores;
using SportsData.Api.Application.Leagues.Queries.GetLeagueWeekContests;
using SportsData.Api.Infrastructure.Auth;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Contest;

namespace SportsData.Api.Application.Leagues;

/// <summary>
/// Operator tools for pick'em leagues: replay a league week's Final contests
/// over SignalR and backfill league week scores for a season. Every action is
/// admin-only: [AdminApiToken] is on the class. The user-facing league surface
/// is <c>UI/Leagues/LeagueController</c> at <c>ui/leagues</c>.
/// </summary>
[ApiController]
[Route("api/leagues")]
[AdminApiToken]
public class LeaguesController : ApiControllerBase
{
    private readonly ILogger<LeaguesController> _logger;

    public LeaguesController(ILogger<LeaguesController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Replays every Final contest on a pickem league's matchup page
    /// for a given week, exercising the Producer → broker → API →
    /// SignalR fan-out path against every MatchupCard the user sees
    /// at GET /ui/leagues/{leagueId}/matchups/{week}. Used to verify
    /// that each card updates independently when its contest's plays
    /// arrive over SignalR — the multi-card analogue of the per-sport
    /// admin debug pages.
    ///
    /// Filtered to Status=="Final" because the per-contest replay
    /// service emits ContestStatusChanged=InProgress as its first
    /// step (no "revert to Scheduled" counterpart). Replaying a
    /// scheduled game would briefly flash it to In Progress in the
    /// UI with no recovery — undesired noise during testing.
    /// </summary>
    [HttpPost("{leagueId:guid}/weeks/{week:int}/replay")]
    public async Task<IActionResult> ReplayLeagueWeekContests(
        [FromRoute] Guid leagueId,
        [FromRoute] int week,
        [FromServices] IGetLeagueWeekContestsQueryHandler queryHandler,
        [FromServices] IContestClientFactory contestClientFactory,
        CancellationToken cancellationToken)
    {
        var queryResult = await queryHandler.ExecuteAsync(
            new GetLeagueWeekContestsQuery(leagueId, week),
            cancellationToken);

        if (!queryResult.IsSuccess)
            return queryResult.ToActionResult().Result!;

        var (sport, contestIds) = queryResult.Value;
        if (contestIds.Count == 0)
        {
            return Accepted(new
            {
                leagueId,
                week,
                sport = sport.ToString(),
                totalMatchups = 0,
                finalContests = 0,
                replaysQueued = 0,
            });
        }

        var client = contestClientFactory.Resolve(sport);

        // Canonical statuses come from Producer in one round-trip;
        // .ToList() is for the cast — GetMatchupsByContestIds takes
        // a List<Guid>, contestIds is IReadOnlyList<Guid>.
        // Direction = Roundel: admin path, no user preference context.
        var matchupsResult = await client.GetMatchupsByContestIds(
            contestIds.ToList(),
            MarkDirection.Roundel,
            cancellationToken);

        if (!matchupsResult.IsSuccess)
            return matchupsResult.ToActionResult().Result!;

        var finalContestIds = (matchupsResult.Value ?? new List<LeagueMatchupDto>())
            .Where(m => string.Equals(m.Status, "Final", StringComparison.OrdinalIgnoreCase))
            .Select(m => m.ContestId)
            .ToList();

        if (finalContestIds.Count == 0)
        {
            _logger.LogInformation(
                "ReplayLeagueWeek: no Final contests to replay. LeagueId={LeagueId}, Week={Week}, Sport={Sport}, TotalMatchups={TotalMatchups}",
                leagueId, week, sport, contestIds.Count);
            return Accepted(new
            {
                leagueId,
                week,
                sport = sport.ToString(),
                totalMatchups = contestIds.Count,
                finalContests = 0,
                replaysQueued = 0,
            });
        }

        // Fan out per-contest replays in parallel. Producer enqueues
        // a Hangfire job per call and returns immediately, so this is
        // a quick burst of HTTP calls — not waiting on play emission.
        var replayResults = await Task.WhenAll(
            finalContestIds.Select(id => client.ReplayContest(id, cancellationToken)));

        var succeeded = replayResults.Count(r => r.IsSuccess);
        var failed = replayResults.Length - succeeded;

        _logger.LogInformation(
            "ReplayLeagueWeek: queued. LeagueId={LeagueId}, Week={Week}, Sport={Sport}, TotalMatchups={TotalMatchups}, FinalContests={FinalContests}, Succeeded={Succeeded}, Failed={Failed}",
            leagueId, week, sport, contestIds.Count, finalContestIds.Count, succeeded, failed);

        return Accepted(new
        {
            leagueId,
            week,
            sport = sport.ToString(),
            totalMatchups = contestIds.Count,
            finalContests = finalContestIds.Count,
            replaysQueued = succeeded,
            replaysFailed = failed,
        });
    }

    /// <summary>
    /// Backfills league week scores for an entire season.
    /// Processes all completed weeks for the specified season year.
    /// Example: POST /api/leagues/scores/backfill?seasonYear=2025
    /// </summary>
    /// <param name="seasonYear">The season year to backfill (e.g., 2024, 2025)</param>
    /// <returns>Summary of backfill operation</returns>
    [HttpPost("scores/backfill")]
    public async Task<ActionResult<BackfillLeagueScoresResult>> BackfillLeagueScores(
        [FromQuery] int seasonYear,
        [FromServices] IBackfillLeagueScoresCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new BackfillLeagueScoresCommand(seasonYear);
        var result = await handler.ExecuteAsync(command, cancellationToken);
        return result.ToActionResult();
    }
}
