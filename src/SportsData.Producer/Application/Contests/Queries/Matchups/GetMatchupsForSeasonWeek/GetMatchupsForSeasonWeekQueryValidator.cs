using FluentValidation;

using SportsData.Core.Common;

namespace SportsData.Producer.Application.Contests.Queries.Matchups.GetMatchupsForSeasonWeek;

/// <summary>
/// An out-of-range value here doesn't error — it silently matches no
/// SeasonWeek rows and returns an empty slate, which reads as "bye week
/// everywhere" to callers. Reject nonsense up front instead.
/// </summary>
public class GetMatchupsForSeasonWeekQueryValidator
    : AbstractValidator<GetMatchupsForSeasonWeekQuery>
{
    /// <summary>ESPN SeasonPhase type codes: 1 Preseason, 2 Regular Season, 3 Postseason, 4 Off Season.</summary>
    private static readonly int[] KnownPhaseTypeCodes = [1, 2, 3, 4];

    public GetMatchupsForSeasonWeekQueryValidator(IDateTimeProvider dateTimeProvider)
    {
        RuleFor(x => x.SeasonYear)
            .GreaterThanOrEqualTo(2000)
            .WithMessage("Season year must be 2000 or later")
            .Must(year => year <= dateTimeProvider.UtcNow().Year + 1)
            .WithMessage("Season year cannot be more than one year in the future");

        RuleFor(x => x.SeasonWeekNumber)
            .InclusiveBetween(1, 30)
            .WithMessage("Week must be between 1 and 30");

        RuleFor(x => x.SeasonPhaseTypeCode)
            .Must(code => KnownPhaseTypeCodes.Contains(code))
            .WithMessage("Season phase type code must be 1 (Preseason), 2 (Regular Season), 3 (Postseason), or 4 (Off Season)");
    }
}
