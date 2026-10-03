using SportsData.Core.Common;

namespace SportsData.Api.Application.Matchups.Jobs.MatchupRecordAudit;

/// <summary>
/// Audit only the league matchups for these contests, in this sport.
/// Contests not in any league are simply absent from the rows and cost
/// nothing; an empty list is a no-op.
/// </summary>
public record MatchupRecordAuditByContestsCommand(
    Sport Sport,
    IReadOnlyList<Guid> ContestIds);