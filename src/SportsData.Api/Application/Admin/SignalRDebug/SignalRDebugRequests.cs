namespace SportsData.Api.Application.Admin.SignalRDebug;

/// <summary>
/// Sandbox ContestIds the debug harness pretends to broadcast for. The
/// server stamps these onto the outbound integration events so the
/// client can't accidentally fan a debug payload out for a real
/// contest the production picks page might be watching.
/// </summary>
public static class SignalRDebugContestIds
{
    public static readonly Guid Football = new("aaaaaaaa-0000-0000-0000-000000000001");
    public static readonly Guid Baseball = new("aaaaaaaa-0000-0000-0000-000000000002");
}

/// <summary>
/// Request body for POST /admin/signalr-debug/contest-status.
/// Sport-neutral lifecycle event. Caller supplies BOTH the raw ESPN status
/// type name (for programmatic branching) and the human-readable description
/// (for display) — same wire shape the live status-doc processor publishes.
/// </summary>
public record DebugContestStatusRequest(
    string Sport,               // "FootballNcaa" / "FootballNfl" / "BaseballMlb"
    string Status,              // "STATUS_SCHEDULED" / "STATUS_IN_PROGRESS" / "STATUS_FINAL"
    string StatusDescription);  // "Scheduled" / "In Progress" / "Final"

/// <summary>
/// Request body for POST /admin/signalr-debug/football-play. Drives a
/// synthetic FootballPlayCompleted — merged play description + scoreboard
/// tick — through the bus → SignalR pipeline.
/// </summary>
public record DebugFootballPlayRequest(
    string Sport,
    string PlayDescription,
    string Period,
    string Clock,
    int AwayScore,
    int HomeScore,
    Guid? PossessionFranchiseSeasonId,
    bool IsScoringPlay,
    int? BallOnYardLine);

/// <summary>
/// Request body for POST /admin/signalr-debug/baseball-play. Drives a
/// synthetic BaseballPlayCompleted — merged play description + scoreboard
/// tick — through the bus → SignalR pipeline.
/// </summary>
public record DebugBaseballPlayRequest(
    string PlayDescription,
    int Inning,
    string HalfInning,
    int AwayScore,
    int HomeScore,
    int Balls,
    int Strikes,
    int Outs,
    bool RunnerOnFirst,
    bool RunnerOnSecond,
    bool RunnerOnThird,
    Guid? AtBatAthleteSeasonId,
    string? AtBatShortName,
    string? AtBatPositionAbbreviation,
    string? AtBatHeadshotUrl,
    Guid? PitchingAthleteSeasonId,
    string? PitchingShortName,
    string? PitchingPositionAbbreviation,
    string? PitchingHeadshotUrl);
