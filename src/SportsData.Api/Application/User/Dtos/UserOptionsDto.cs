namespace SportsData.Api.Application.User.Dtos;

/// <summary>
/// Typed projection of a user's <c>UserOption</c> rows (known keys only —
/// see <c>UserOptionKeys</c>). Absent or unparsable rows yield each option's
/// default, so clients always receive a full, well-typed set.
/// </summary>
public record UserOptionsDto
{
    /// <summary>
    /// Default false: gambling content (spreads, totals, odds) renders only
    /// where the league's pick type requires it (ATS/OU) until the user opts
    /// in. Per-user only — never a league setting.
    /// </summary>
    public bool ShowGamblingContent { get; init; }
}
