namespace SportsData.Core.Common;

/// <summary>
/// Identifies which of the generated sportDeets team-mark designs the caller
/// wants surfaced. See docs/team-mark-design-brief.md for the three directions
/// and src/marks/ for the engine. Roundel = 0 makes it the default value for
/// uninitialized fields, matching the system default direction.
/// </summary>
public enum MarkDirection
{
    Roundel = 0,
    Shield = 1,
    Hex = 2
}
