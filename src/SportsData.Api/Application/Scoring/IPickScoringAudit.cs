namespace SportsData.Api.Application.Scoring;

/// <summary>
/// Per-contest audit work. Enqueued by
/// <see cref="SportsData.Api.Application.Jobs.PickScoringAuditJob"/> once per
/// distinct contest with scored picks. Re-runs <see cref="IPickScoringService"/>
/// on a working copy of each scored pick and compares to stored values;
/// corrects mismatches in place and fans out league-week rescoring.
///
/// See <c>docs/pick-scoring-audit-job.md</c> for design rationale.
/// </summary>
public interface IPickScoringAudit
{
    Task Process(AuditContestCommand command);
}
