namespace SportsData.Api.Application.Common.Enums;

/// <summary>
/// Until when a league accepts new members. Chosen by the commissioner at
/// creation (post-creation editing is deferred with the league-settings-edit
/// feature — settings are create-only today). Applies to ALL leagues — public
/// browse joins and invite-link joins flow through the same gate, so a shared
/// invite link to a closed league dies with the listing.
///
/// Deliberately two values in v1. The close moment for
/// <see cref="CloseAtFirstGame"/> is DERIVED at read time from the league's
/// matchups (kickoff times move after slates generate; a stored deadline
/// would rot). A future derived option — open through the league's
/// DropLowWeeksCount dropped weeks — slots in as a third value with no
/// migration. See docs/features/league-join-policy-and-discovery.md.
/// </summary>
public enum JoinPolicy
{
    /// <summary>Joinable while the league is live (until deactivation).</summary>
    Open = 0,

    /// <summary>
    /// Roster locks when play begins: closed once the league's first
    /// scheduled contest starts. For a single-day league that is first
    /// pitch; for a season league, week-1 kickoff inside its window.
    /// An empty slate (built asynchronously after creation) means nothing
    /// has started, so the league is open.
    /// </summary>
    CloseAtFirstGame = 1
}
