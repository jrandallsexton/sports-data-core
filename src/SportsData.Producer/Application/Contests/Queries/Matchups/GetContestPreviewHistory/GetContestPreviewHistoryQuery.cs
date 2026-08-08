namespace SportsData.Producer.Application.Contests.Queries.Matchups.GetContestPreviewHistory;

/// <summary>
/// Historical context for a matchup preview: last N head-to-head meetings
/// (franchise-level, cross-season) and each team's last N games of the
/// prior season. Defaults of 5/5 per the design doc
/// (docs/metrics-modeling/matchup-preview-data-inputs.md §3b/3c).
/// </summary>
public record GetContestPreviewHistoryQuery(
    Guid ContestId,
    int MeetingCount = 5,
    int RecentGameCount = 5);
