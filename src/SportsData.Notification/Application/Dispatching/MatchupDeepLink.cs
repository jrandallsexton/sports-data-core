using SportsData.Core.Common;

namespace SportsData.Notification.Application.Dispatching;

/// <summary>
/// Builds the FCM data payload that sends a notification tap to a specific
/// matchup. Server-side twin of the mobile client's
/// <c>src/utils/deepLinks.ts</c> — the wire contract lives in exactly one
/// place per side, so adding a notification kind that lands on a game page
/// doesn't re-derive the key names.
///
/// <para>
/// Shape follows the kind/id convention established by
/// <c>UserInvitedToPickemGroupConsumer</c>. Sport travels as the backend enum
/// NAME ("FootballNcaa"), not route segments: the client maps it through its
/// own resolveSportLeague, keeping URL conventions client-owned.
/// </para>
///
/// <para>
/// Every value is a string because FCM data payloads are string maps; the
/// client parses <c>week</c> back to a number and drops it when unparseable.
/// Optional values are OMITTED rather than sent empty, so the client's
/// "is this key present" checks stay meaningful.
/// </para>
/// </summary>
public static class MatchupDeepLink
{
    /// <summary>Line moved on a contest the user picked.</summary>
    public const string OddsChangedKind = "OddsChanged";

    /// <summary>The user's pick was scored.</summary>
    public const string PickScoredKind = "PickScored";

    public static Dictionary<string, string> Build(
        string kind,
        Guid contestId,
        Sport sport,
        Guid? leagueId = null,
        int? week = null)
    {
        var data = new Dictionary<string, string>
        {
            ["kind"] = kind,
            ["target"] = "matchup",
            ["contestId"] = contestId.ToString(),
            ["sport"] = sport.ToString()
        };

        // Scopes the game page to the user's league so their pick renders
        // alongside the contest.
        if (leagueId is { } league && league != Guid.Empty)
            data["leagueId"] = league.ToString();

        if (week is { } w)
            data["week"] = w.ToString();

        return data;
    }
}
