namespace SportsData.Api.Application.Matchups.Jobs.MatchupRecordAudit;

/// <param name="Unresolved">
/// Contests Producer could not derive a record for. Non-zero means the two
/// services disagree about what exists, which is worth looking at.
/// </param>
public record MatchupRecordAuditResult(
    int Examined,
    int Corrected,
    int Unresolved);