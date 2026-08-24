using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Scoring;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.Jobs;

/// <summary>
/// Nightly per-sport audit of historical pick scoring. Re-runs scoring
/// against current canonical data for every previously-scored pick of the
/// targeted sport; corrects mismatches in place and resets picks scored
/// against contests that aren't actually finalized.
///
/// Why per-sport: each instance fans out to one sport's Producer pod via
/// <see cref="IContestClientFactory.Resolve"/>. Sport-scoping gives
/// operational isolation (a failing MLB audit doesn't block NCAAFB),
/// trivial Seq segmentation, and aligns with the per-sport Producer
/// boundary. See <c>docs/pick-scoring-audit-job.md</c>.
///
/// Doesn't implement <see cref="SportsData.Core.Common.Jobs.IAmARecurringJob"/>
/// because that interface requires a parameterless <c>ExecuteAsync()</c> and
/// here we need the sport. The interface has no other consumers in the
/// codebase (marker only), so dropping it costs nothing.
/// </summary>
public class PickScoringAuditJob
{
    private readonly ILogger<PickScoringAuditJob> _logger;
    private readonly AppDataContext _dataContext;
    private readonly IProvideBackgroundJobs _backgroundJobProvider;

    public PickScoringAuditJob(
        ILogger<PickScoringAuditJob> logger,
        AppDataContext dataContext,
        IProvideBackgroundJobs backgroundJobProvider)
    {
        _logger = logger;
        _dataContext = dataContext;
        _backgroundJobProvider = backgroundJobProvider;
    }

    public async Task ExecuteAsync(Sport sport)
    {
        var correlationId = Guid.NewGuid();

        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   ["CorrelationId"] = correlationId,
                   ["Sport"] = sport
               }))
        {
            _logger.LogInformation("{JobName} began.", nameof(PickScoringAuditJob));

            // Sport-scoped candidate selection: every distinct ContestId with
            // at least one scored-but-UNAUDITED pick whose PickemGroup is for
            // this sport. SQL-level filter so we don't pull other sports'
            // contests into memory just to discard them.
            //
            // AuditedUtc is the watermark that keeps this bounded. Without it
            // the job re-audited every pick ever scored on every run — 1,284
            // contests on 2026-08-24, almost all settled 2025 games, growing
            // every week forever, each one its own Hangfire job and Producer
            // round trip. Re-auditing is still driven by DATA CHANGE rather
            // than by age: the watermark is cleared when a score correction or
            // a re-enrichment lands, so a fix that arrives a year later is
            // still caught. (A time window would not have caught the ATS
            // re-enrichment, which corrected 2025 games in August 2026.)
            var contestIds = await _dataContext.UserPicks
                .Where(p => p.ScoredAt != null && p.AuditedUtc == null)
                .Join(_dataContext.PickemGroups,
                    p => p.PickemGroupId,
                    g => g.Id,
                    (p, g) => new { p.ContestId, g.Sport })
                .Where(x => x.Sport == sport)
                .Select(x => x.ContestId)
                .Distinct()
                .ToListAsync();

            _logger.LogInformation(
                "Found {Count} distinct contests to audit.",
                contestIds.Count);

            var enqueuedCount = 0;
            foreach (var contestId in contestIds)
            {
                try
                {
                    var cmd = new AuditContestCommand(contestId, sport, correlationId);
                    _backgroundJobProvider.Enqueue<IPickScoringAudit>(p => p.Process(cmd));
                    enqueuedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to enqueue audit for contest {ContestId}. Tomorrow's run will retry.",
                        contestId);
                }
            }

            _logger.LogInformation(
                "{JobName} ended. EnqueuedCount={Count}.",
                nameof(PickScoringAuditJob), enqueuedCount);
        }
    }
}
