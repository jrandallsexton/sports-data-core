namespace SportsData.Producer.Application.Contests.Queries.GetContestPlayLog;

/// <summary>
/// Full play-by-play log for a contest. The overview endpoint trims to
/// key/scoring plays only to keep its payload small; this endpoint exists
/// as the on-demand "Show all plays" expansion (typically 500+ rows for an
/// MLB game).
/// </summary>
public record GetContestPlayLogQuery(Guid ContestId);
