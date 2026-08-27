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

    /// <summary>
    /// Single projection from raw UserOption key/value rows — shared by
    /// GetMe (which embeds options on the user DTO so clients need no
    /// second round-trip) and the standalone options endpoint (kept for
    /// mobile). Unknown keys ignored; absent/unparsable values take each
    /// option's default.
    /// </summary>
    public static UserOptionsDto FromRows(IEnumerable<KeyValuePair<string, string>> rows)
    {
        var byKey = new Dictionary<string, string>(rows, StringComparer.Ordinal);
        return new UserOptionsDto
        {
            ShowGamblingContent = ParseBool(byKey, UserOptionKeys.ShowGamblingContent, defaultValue: false),
        };
    }

    private static bool ParseBool(
        IReadOnlyDictionary<string, string> byKey,
        string key,
        bool defaultValue)
        => byKey.TryGetValue(key, out var raw) && bool.TryParse(raw, out var parsed)
            ? parsed
            : defaultValue;
}
