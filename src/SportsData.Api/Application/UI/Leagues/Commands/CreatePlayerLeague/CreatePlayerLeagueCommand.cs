namespace SportsData.Api.Application.UI.Leagues.Commands.CreatePlayerLeague;

/// <summary>
/// Create-request for a PLAYER Pick'em league. Deliberately not a
/// <see cref="CreateLeagueRequestBase"/> descendant: player leagues have
/// no pick type, tiebreakers, confidence points, or grouping filters —
/// the roster IS the game. One request covers both football sports via
/// <see cref="Sport"/> rather than the per-sport endpoint trio, because
/// nothing else differs per sport.
/// </summary>
public class CreatePlayerLeagueCommand
{
    /// <summary>"FootballNcaa" | "FootballNfl".</summary>
    public required string Sport { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public bool IsPublic { get; set; }

    /// <summary>JoinPolicy enum name; absent = Open (matches team-league default).</summary>
    public string? JoinPolicy { get; set; }

    /// <summary>Defaults to the current year.</summary>
    public int? SeasonYear { get; set; }

    /// <summary>Inclusive window start; null = from the start of the season.</summary>
    public DateTime? StartsOn { get; set; }

    /// <summary>Inclusive window end; null = through the end of the season.</summary>
    public DateTime? EndsOn { get; set; }

    /// <summary>
    /// UTC-kind-stamped start. JSON date-only values ("2026-08-27")
    /// deserialize with Kind=Unspecified, which Npgsql refuses to write
    /// to timestamptz; values are semantically UTC per project
    /// convention, so stamp the kind rather than converting.
    /// </summary>
    public DateTime? EffectiveStartsOn =>
        StartsOn is { Kind: DateTimeKind.Unspecified } startsOn
            ? DateTime.SpecifyKind(startsOn, DateTimeKind.Utc)
            : StartsOn;

    /// <summary>
    /// Midnight-normalized end (same contract as the team-league flow):
    /// a date-only value becomes end-of-day UTC so "inclusive" holds.
    /// Non-midnight values still get their kind stamped UTC (Npgsql
    /// rejects Kind=Unspecified on timestamptz).
    /// </summary>
    public DateTime? EffectiveEndsOn =>
        EndsOn is { TimeOfDay.Ticks: 0 } endsOn
            ? DateTime.SpecifyKind(endsOn.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc)
            : EndsOn is { Kind: DateTimeKind.Unspecified } raw
                ? DateTime.SpecifyKind(raw, DateTimeKind.Utc)
                : EndsOn;
}
