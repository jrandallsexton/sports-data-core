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

            // Sport-scoped candidate selection: every distinct ContestId
            // that has at least one scored pick whose PickemGroup is for
            // this sport. SQL-level filter so we don't pull other sports'
            // contests into memory just to discard them.
            var contestIds = await _dataContext.UserPicks
                .Where(p => p.ScoredAt != null)
                .Join(_dataContext.PickemGroups,
                    p => p.PickemGroupId,
                    g => g.Id,
                    (p, g) => new { p.ContestId, g.Sport })
                .Where(x => x.Sport == sport)
                .Select(x => x.ContestId)
                .Distinct()
                .ToListAsync();

            _logger.LogInformation(
                "Found {Count} distinct contests to audit for {Sport}.",
                contestIds.Count, sport);

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
