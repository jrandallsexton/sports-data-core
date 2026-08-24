using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;

namespace SportsData.Api.Application.Scoring;

public interface IInvalidatePickAudits
{
    Task<int> InvalidateForContestAsync(Guid contestId, string reason, CancellationToken cancellationToken = default);
}

/// <summary>
/// Clears the audit watermark on a contest's picks so the nightly audit
/// re-verifies them.
///
/// This is what keeps the watermark honest. The audit selects only picks with
/// a null AuditedUtc, so without invalidation a corrected contest would stay
/// invisible forever. Re-auditing is therefore driven by DATA CHANGE, not by
/// age — a correction landing a year after the game still triggers a re-audit,
/// which no time-based window would catch.
///
/// Called from the two events that can move a scored pick's inputs:
///   - ContestScoreChanged — the scores themselves were corrected.
///   - ContestFinalized — enrichment (re)ran, which is how a spread-winner or
///     over/under fix arrives WITHOUT any score changing. This path matters
///     more than it looks: PickScoringProcessor short-circuits when every pick
///     on the contest is already scored, so a re-enrichment of settled picks
///     does NOT re-score them. The audit is the only thing that catches it,
///     and it only looks at unwatermarked picks.
/// </summary>
public class PickAuditInvalidator : IInvalidatePickAudits
{
    private readonly ILogger<PickAuditInvalidator> _logger;
    private readonly AppDataContext _dataContext;

    public PickAuditInvalidator(
        ILogger<PickAuditInvalidator> logger,
        AppDataContext dataContext)
    {
        _logger = logger;
        _dataContext = dataContext;
    }

    public async Task<int> InvalidateForContestAsync(
        Guid contestId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        // Loaded rather than ExecuteUpdate: a contest carries a handful of
        // picks per league, so the set is small, and the unit tests run on the
        // EF InMemory provider, which has no ExecuteUpdate support.
        var picks = await _dataContext.UserPicks
            .Where(p => p.ContestId == contestId && p.AuditedUtc != null)
            .ToListAsync(cancellationToken);

        if (picks.Count == 0)
        {
            return 0;
        }

        foreach (var pick in picks)
        {
            // Only the watermark is touched. ModifiedUtc tracks when the
            // pick's SCORING changed; queueing a re-audit is not that.
            pick.AuditedUtc = null;
        }

        await _dataContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Pick audit invalidated. ContestId={ContestId}, PickCount={PickCount}, Reason={Reason}",
            contestId, picks.Count, reason);

        return picks.Count;
    }
}
