namespace SportsData.Api.Application.Common.Enums;

/// <summary>
/// Which game a PickemGroup plays. PRODUCT RULE (operator, 2026-08-26):
/// a league is exactly ONE game — a roster league is a different league,
/// not a companion mode on a team-pick league (two scoring systems in
/// one group is asking for trouble; invite the same friends to a second
/// league instead). An enum rather than capability flags because the
/// games are mutually exclusive — flags would make the invalid
/// both-enabled state representable and force every consumer to police
/// it. TeamPickem is deliberately 0 so every existing row is correct by
/// default. Distinct from PickType, which answers "how is a TEAM pick
/// scored?" within a TeamPickem group.
/// </summary>
public enum GroupType
{
    TeamPickem = 0,
    PlayerPickem = 1,
}
