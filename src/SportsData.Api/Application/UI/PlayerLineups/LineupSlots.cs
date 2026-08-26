namespace SportsData.Api.Application.UI.PlayerLineups;

/// <summary>
/// The fixed v1 Player Pick'em lineup shape and its eligibility rules —
/// the server-side authority the UI's rosterLogic modules mirror.
/// 'DEF' is reserved until the team-defense picker exists; a
/// commissioner-configurable shape is a future option that lives beside
/// PickemGroup.GroupType (one game per league).
/// See docs/features/player-pickem/roster-persistence.md.
/// </summary>
public static class LineupSlots
{
    public static readonly IReadOnlyDictionary<string, string[]> Eligibility =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["QB"] = ["QB"],
            ["RB1"] = ["RB"],
            ["RB2"] = ["RB"],
            ["WR1"] = ["WR"],
            ["WR2"] = ["WR"],
            ["TE"] = ["TE"],
            ["FLEX"] = ["RB", "WR", "TE"],
            ["K"] = ["K"],
        };

    public static bool IsValidSlot(string slotId) => Eligibility.ContainsKey(slotId);

    public static bool IsEligible(string slotId, string position) =>
        Eligibility.TryGetValue(slotId, out var positions) &&
        positions.Contains(position, StringComparer.OrdinalIgnoreCase);
}
