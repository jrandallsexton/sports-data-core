using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.Admin;
using SportsData.Api.Application.Matchups.Commands.BackfillMatchupOddsPricing;
using SportsData.Api.Application.Matchups.Commands.RefreshWeekMatchups;
using SportsData.Api.Application.Matchups.Jobs.MatchupRecordAudit;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.Matchups;

/// <summary>
/// Operator tools for league matchups (PickemGroupMatchup): refresh a week's
/// record snapshots, audit them against canonical data, and backfill odds
/// pricing. Every action is admin-only: [AdminApiToken] is on the class.
/// </summary>
[ApiController]
[Route("api/matchups")]
[AdminApiToken]
public class MatchupsController : ApiControllerBase
{
    /// <remarks>
    /// Those records are a COPY taken when the week was generated, not a
    /// live read — the league-week query never asks Producer for them. So
    /// anything that corrects a FranchiseSeason after generation (a late
    /// enrichment pass, a re-finalized contest) leaves the cards showing
    /// the values frozen at generation time, and no amount of cache
    /// </remarks>
    /// <summary>
    /// Re-runs the matchup scheduler over an already-generated week so the
    /// record snapshots on PickemGroupMatchup are rewritten from canonical
    /// data. See RefreshWeekMatchupsCommandHandler for why that is needed
    /// and why it is safe to re-run.
    /// </summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<RefreshWeekMatchupsResponse>> RefreshWeekMatchups(
        [FromServices] IRefreshWeekMatchupsCommandHandler handler,
        // Precise week identity, and preferred: week NUMBERS are ambiguous
        // across phase AND sport.
        [FromQuery] Guid? seasonWeekId = null,
        [FromQuery] int? seasonYear = null,
        [FromQuery] int? seasonWeek = null,
        // Narrows the year/week form; ignored when seasonWeekId is given.
        [FromQuery] Sport? sport = null,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.ExecuteAsync(
            new RefreshWeekMatchupsCommand
            {
                SeasonWeekId = seasonWeekId,
                SeasonYear = seasonYear,
                SeasonWeek = seasonWeek,
                Sport = sport
            },
            cancellationToken);

        return result.ToActionResult();
    }

    /// <summary>
    /// Recomputes the record snapshots on PickemGroupMatchup from prior
    /// finalized outcomes, correcting only rows that differ.
    /// </summary>
    /// <remarks>
    /// The league card reads the snapshot and never derives (#769), so a
    /// wrong snapshot stays wrong until something rewrites it. #771 stopped
    /// new damage but repaired none, and rows predating the
    /// MatchupRecordSnapshots migration were never backfilled at all.
    /// <para>
    /// Safe and worth re-running: the derivation counts only finalized
    /// contests, so anything still cycling through the enrichment audit
    /// leaves its teams a game light until that settles.
    /// </para>
    /// </remarks>
    [HttpPost("audit-records")]
    public async Task<IActionResult> AuditMatchupRecords(
        [FromServices] IAuditMatchupRecords processor,
        [FromQuery] int seasonYear,
        // Omit to audit every week of the season year.
        [FromQuery] int? seasonWeek = null,
        [FromQuery] Sport sport = Sport.FootballNcaa)
    {
        var result = await processor.Process(
            new MatchupRecordAuditCommand(sport, seasonYear, seasonWeek));

        return Ok(new
        {
            sport = sport.ToString(),
            seasonYear,
            seasonWeek,
            examined = result.Examined,
            corrected = result.Corrected,
            unresolved = result.Unresolved
        });
    }

    /// <summary>
    /// Populates odds pricing (per-team moneyline and spread price, over/under
    /// prices) on every PickemGroupMatchup: enqueues one background job per
    /// distinct contest, each priced from its sport's Producer. Returns 202
    /// with the correlation id and per-sport job counts. Writes only values
    /// the Producer supplies (never erases); idempotent, safe to re-run.
    /// Example: POST /api/matchups/backfill-odds-pricing
    /// </summary>
    [HttpPost("backfill-odds-pricing")]
    public async Task<ActionResult<BackfillMatchupOddsPricingResult>> BackfillMatchupOddsPricing(
        [FromServices] IBackfillMatchupOddsPricingCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(
            new BackfillMatchupOddsPricingCommand(),
            cancellationToken);
        return result.ToActionResult();
    }
}
