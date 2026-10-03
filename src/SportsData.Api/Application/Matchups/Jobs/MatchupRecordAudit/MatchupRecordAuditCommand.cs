using SportsData.Core.Common;

namespace SportsData.Api.Application.Matchups.Jobs.MatchupRecordAudit;

/// <param name="SeasonWeek">Null audits every week of the season year.</param>
public record MatchupRecordAuditCommand(
    Sport Sport,
    int SeasonYear,
    int? SeasonWeek = null);