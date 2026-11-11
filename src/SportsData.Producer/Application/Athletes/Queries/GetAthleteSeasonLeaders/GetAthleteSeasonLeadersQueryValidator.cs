using FluentValidation;

using SportsData.Core.Common;

namespace SportsData.Producer.Application.Athletes.Queries.GetAthleteSeasonLeaders;

/// <summary>
/// Season-year bounds match the sibling athlete query validators. Category
/// and stat are ESPN names, matched exactly; an unknown name returns an
/// empty board rather than an error, since the set of names is ESPN's.
/// </summary>
public class GetAthleteSeasonLeadersQueryValidator : AbstractValidator<GetAthleteSeasonLeadersQuery>
{
    public GetAthleteSeasonLeadersQueryValidator(IDateTimeProvider dateTimeProvider)
    {
        RuleFor(x => x.SeasonYear)
            .GreaterThanOrEqualTo(2000)
            .WithMessage("Season year must be 2000 or later")
            .Must(year => year <= dateTimeProvider.UtcNow().Year + 1)
            .WithMessage("Season year cannot be more than one year in the future");

        RuleFor(x => x.Category)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("Category is required (at most 100 characters)");

        RuleFor(x => x.Stat)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("Stat is required (at most 100 characters)");

        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 50)
            .WithMessage("Limit must be between 1 and 50");
    }
}
