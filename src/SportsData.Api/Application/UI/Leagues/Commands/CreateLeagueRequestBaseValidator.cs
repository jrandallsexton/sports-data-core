using FluentValidation;

using SportsData.Api.Application.Common.Enums;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Leagues.Commands;

/// <summary>
/// Shared validation for every sport-specific create-league request. Derived
/// validators add only sport-specific rules (e.g. division/conference slug lists
/// and, for NCAA, the optional RankingFilter). Helpers are <c>protected static</c>
/// so derived classes can reuse them in their own rules.
/// </summary>
public abstract class CreateLeagueRequestBaseValidator<TRequest> : AbstractValidator<TRequest>
    where TRequest : CreateLeagueRequestBase
{
    protected CreateLeagueRequestBaseValidator(IDateTimeProvider dateTimeProvider)
    {
        ArgumentNullException.ThrowIfNull(dateTimeProvider);

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("League name is required.");

        RuleFor(x => x.PickType)
            .Must(IsDefinedEnumName<PickType>)
            .WithMessage(x => $"Invalid pick type: {x.PickType}");

        RuleFor(x => x.TiebreakerType)
            .Must(IsDefinedEnumName<TiebreakerType>)
            .WithMessage(x => $"Invalid tiebreaker type: {x.TiebreakerType}");

        RuleFor(x => x.TiebreakerTiePolicy)
            .Must(IsDefinedEnumName<TiebreakerTiePolicy>)
            .WithMessage(x => $"Invalid tiebreaker tie policy: {x.TiebreakerTiePolicy}");

        // Compare against EffectiveEndsOn so a midnight-aligned EndsOn normalizes to
        // end-of-day first. That lets a naive caller send the same date for start and
        // end ("a window covering Sept 30") while still rejecting arbitrarily equal
        // non-midnight timestamps (zero-width windows).
        RuleFor(x => x)
            .Must(x => !x.StartsOn.HasValue
                || !x.EffectiveEndsOn.HasValue
                || x.StartsOn.Value < x.EffectiveEndsOn.Value)
            .WithName(nameof(CreateLeagueRequestBase.EndsOn))
            .WithMessage("EndsOn must be after StartsOn.");

        // Reject windows whose end has already passed — no pickable games can
        // exist in such a window. Windows that straddle "now" (e.g. a single-day
        // league created at noon where the morning games already started) are
        // allowed: the matchup processor's [StartsOn, EndsOn] filter still grabs
        // the in-window games and the pick-lock logic handles per-game
        // pickability. EndsOn-only check is sufficient because the existing
        // StartsOn < EffectiveEndsOn rule above already prevents a degenerate
        // window. UI sibling guards in PR-C (web `min={today}` + mobile Zod
        // superRefine) prevent users from constructing this from the form; this
        // is the trust boundary for admin / direct-API callers.
        RuleFor(x => x)
            .Must(x => !x.EffectiveEndsOn.HasValue || x.EffectiveEndsOn.Value > dateTimeProvider.UtcNow())
            .WithName(nameof(CreateLeagueRequestBase.EndsOn))
            .WithMessage("EndsOn can't be in the past.");
    }

    /// <summary>
    /// Ensures the string parses to the enum *and* the resulting value is one of the
    /// declared members. Plain <c>Enum.TryParse</c> accepts any numeric string (e.g. "999"),
    /// which <c>Enum.IsDefined</c> filters out.
    /// </summary>
    protected static bool IsDefinedEnumName<TEnum>(string? value) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed)
            && Enum.IsDefined(typeof(TEnum), parsed);
    }

    /// <summary>Case-insensitive duplicate detection for slug-style string lists.</summary>
    protected static bool HasNoDuplicates(IEnumerable<string> values) =>
        values is null
        || values.Count() == values.Distinct(StringComparer.OrdinalIgnoreCase).Count();
}
