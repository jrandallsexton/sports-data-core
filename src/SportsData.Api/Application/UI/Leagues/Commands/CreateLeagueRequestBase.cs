namespace SportsData.Api.Application.UI.Leagues.Commands;

/// <summary>
/// Shared fields for all sport-specific create-league requests.
/// </summary>
public abstract class CreateLeagueRequestBase
{
    public required string Name { get; set; }

    public string? Description { get; set; }

    public required string PickType { get; set; }

    public bool UseConfidencePoints { get; set; }

    public required string TiebreakerType { get; set; }

    public required string TiebreakerTiePolicy { get; set; }

    public bool IsPublic { get; set; }

    public int? DropLowWeeksCount { get; set; }

    /// <summary>
    /// The season year. Defaults to the current year if not specified.
    /// </summary>
    public int? SeasonYear { get; set; }

    /// <summary>
    /// Inclusive start of the league window. Null = from the start of the season.
    /// </summary>
    public DateTime? StartsOn { get; set; }

    /// <summary>
    /// Inclusive end of the league window. Null = through the end of the season.
    /// </summary>
    public DateTime? EndsOn { get; set; }

    /// <summary>
    /// Normalized form of <see cref="EndsOn"/> for persistence and querying.
    /// When the caller supplies a midnight timestamp (i.e. a date with no time
    /// component), this property returns end-of-day so "inclusive end" behaves
    /// as documented. Values with an explicit time (e.g. the FE's
    /// <c>YYYY-MM-DDT23:59:59Z</c>) pass through unchanged.
    /// <para>
    /// The computed end-of-day is explicitly stamped with <see cref="DateTimeKind.Utc"/>
    /// so Npgsql can write it to a <c>timestamp with time zone</c> column without
    /// drifting when the caller supplied a <see cref="DateTimeKind.Unspecified"/> input.
    /// Pass-through values are left alone per the project convention of trusting
    /// inbound DateTime values as UTC.
    /// </para>
    /// </summary>
    public DateTime? EffectiveEndsOn =>
        EndsOn is { TimeOfDay.Ticks: 0 } endsOn
            ? DateTime.SpecifyKind(endsOn.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc)
            : EndsOn;
}
