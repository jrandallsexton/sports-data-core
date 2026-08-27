using FluentValidation;

using SportsData.Core.Common;

namespace SportsData.Api.Application.Athletes.Queries.GetPickemAthletes;

/// <summary>
/// Numeric-bounds gate in front of the Producer relay — without it a
/// request like seasonYear=0&amp;week=-1 rides all the way to Producer just
/// to come back as a validation failure. Bounds mirror Producer's
/// GetAthleteMatchupSummariesQueryValidator. Sport/league route segments
/// are validated by ModeMapper in the handler; position membership is
/// validated by Producer, whose whitelist and stat mapping are one
/// dictionary.
/// </summary>
public class GetPickemAthletesQueryValidator : AbstractValidator<GetPickemAthletesQuery>
{
    public GetPickemAthletesQueryValidator(IDateTimeProvider dateTimeProvider)
    {
        RuleFor(x => x.Position)
            .NotEmpty()
            .WithMessage("Position is required");

        RuleFor(x => x.SeasonYear)
            .GreaterThanOrEqualTo(2000)
            .WithMessage("Season year must be 2000 or later")
            .Must(year => year <= dateTimeProvider.UtcNow().Year + 1)
            .WithMessage("Season year cannot be more than one year in the future");

        RuleFor(x => x.Week)
            .InclusiveBetween(1, 30)
            .WithMessage("Week must be between 1 and 30");

        RuleFor(x => x.Phase)
            .Must(p => p is "preseason" or "regular" or "postseason")
            .WithMessage("Phase must be 'preseason', 'regular', or 'postseason'");
    }
}
