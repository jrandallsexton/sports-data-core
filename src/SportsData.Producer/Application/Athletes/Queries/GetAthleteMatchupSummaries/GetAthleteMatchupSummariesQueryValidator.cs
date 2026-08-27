using FluentValidation;

using SportsData.Core.Common;

namespace SportsData.Producer.Application.Athletes.Queries.GetAthleteMatchupSummaries;

/// <summary>
/// Season-year bounds match the sibling sourcing validators (2000 floor;
/// at most one year out). Week 1–30 comfortably covers any phase's week
/// numbering. Position is validated against the handler's supported set
/// inside the handler itself — the whitelist and the stat mapping are one
/// dictionary, and splitting them would let the two drift.
/// </summary>
public class GetAthleteMatchupSummariesQueryValidator
    : AbstractValidator<GetAthleteMatchupSummariesQuery>
{
    public GetAthleteMatchupSummariesQueryValidator(IDateTimeProvider dateTimeProvider)
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

        RuleFor(x => x.SeasonPhaseTypeCode)
            .InclusiveBetween(1, 3)
            .WithMessage("Season phase type code must be 1 (Preseason), 2 (Regular Season), or 3 (Postseason)");
    }
}
