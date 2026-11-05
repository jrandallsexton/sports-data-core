using FluentValidation;

using SportsData.Core.Common;

namespace SportsData.Producer.Application.Contests.Commands.FinalizeContestsBySeasonYear;

/// <summary>
/// 2000 is a sourced season (NCAA history runs through 2000), so the floor is
/// inclusive; same rule as RequestFranchiseSeasonSourcingCommandValidator.
/// </summary>
public class FinalizeContestsBySeasonYearCommandValidator : AbstractValidator<FinalizeContestsBySeasonYearCommand>
{
    public FinalizeContestsBySeasonYearCommandValidator(IDateTimeProvider dateTimeProvider)
    {
        RuleFor(x => x.Sport)
            .IsInEnum()
            .WithMessage("Sport must be a valid enum value");

        RuleFor(x => x.SeasonYear)
            .GreaterThanOrEqualTo(2000)
            .WithMessage("Season year must be 2000 or later")
            .Must(year => year <= dateTimeProvider.UtcNow().Year + 1)
            .WithMessage("Season year cannot be more than one year in the future");

        RuleFor(x => x.CorrelationId)
            .NotEmpty()
            .WithMessage("CorrelationId is required");
    }
}
