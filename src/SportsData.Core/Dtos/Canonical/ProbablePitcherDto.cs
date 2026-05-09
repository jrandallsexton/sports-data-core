namespace SportsData.Core.Dtos.Canonical;

/// <summary>
/// Probable starting pitcher for an MLB matchup. Optional — non-MLB
/// matchups serialize this as null. UI conditionally renders.
/// </summary>
public sealed class ProbablePitcherDto
{
    public string DisplayName { get; set; } = default!;

    public string? HeadshotUrl { get; set; }
}
