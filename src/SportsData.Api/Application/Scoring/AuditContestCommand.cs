using SportsData.Core.Common;

namespace SportsData.Api.Application.Scoring;

public class AuditContestCommand
{
    public Guid ContestId { get; set; }

    // Captured up-front by PickScoringAuditJob so the processor doesn't
    // repeat the PickemGroupMatchups → PickemGroup.Sport join per contest.
    // The job already filtered to this sport at the candidate-selection
    // SQL layer; the processor trusts that.
    public Sport Sport { get; set; }

    public Guid CorrelationId { get; set; }

    public AuditContestCommand()
    {
        CorrelationId = Guid.NewGuid();
    }

    public AuditContestCommand(Guid contestId, Sport sport)
        : this()
    {
        ContestId = contestId;
        Sport = sport;
    }

    public AuditContestCommand(Guid contestId, Sport sport, Guid correlationId)
    {
        ContestId = contestId;
        Sport = sport;
        CorrelationId = correlationId;
    }
}
