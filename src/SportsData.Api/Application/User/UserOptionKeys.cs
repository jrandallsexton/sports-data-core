namespace SportsData.Api.Application.User;

/// <summary>
/// Registry of known per-user option keys — the single source of truth for
/// what the <c>UserOption</c> key/value rows may contain. Adding an option:
/// add a constant here, a typed field on <c>UserOptionsDto</c>, and map it in
/// the Get/Update handlers. No migration. See docs/features/user-options.md.
/// </summary>
public static class UserOptionKeys
{
    /// <summary>
    /// Opt-IN to gambling-related content (spreads, totals, odds) on surfaces
    /// that don't functionally require it. Default false: Straight-Up leagues
    /// hide lines until the user opts in; ATS and O/U leagues always show them
    /// (the lines are the game). Stored as bool.ToString().
    /// </summary>
    public const string ShowGamblingContent = "ShowGamblingContent";
}
