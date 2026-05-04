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
/// Sport-neutral lifecycle event: Scheduled / InProgress / Final.
/// </summary>
public record DebugContestStatusRequest(
    string Sport,    // "FootballNcaa" / "FootballNfl" / "BaseballMlb"
    string Status);  // "Scheduled" / "InProgress" / "Final"

/// <summary>
/// Request body for POST /admin/signalr-debug/football-state.
/// </summary>
public record DebugFootballStateRequest(
    string Sport,
    string Period,
    string Clock,
    int AwayScore,
    int HomeScore,
    Guid? PossessionFranchiseSeasonId,
    bool IsScoringPlay,
    int? BallOnYardLine);

/// <summary>
/// Request body for POST /admin/signalr-debug/play-completed.
/// Sport-neutral per-play log event — payload mirrors what Producer
/// publishes when a new CompetitionPlay lands while the contest is live.
/// </summary>
public record DebugContestPlayCompletedRequest(
    string Sport,             // "FootballNcaa" / "FootballNfl" / "BaseballMlb"
    string PlayDescription);

/// <summary>
/// Request body for POST /admin/signalr-debug/baseball-state.
/// </summary>
public record DebugBaseballStateRequest(
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
    Guid? AtBatAthleteId,
    Guid? PitchingAthleteId);
