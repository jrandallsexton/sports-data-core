using FluentValidation;

using SportsData.Core.Common;

namespace SportsData.Producer.Application.Contests.Commands;

public class RefreshContestsBySeasonYearCommandValidator : AbstractValidator<RefreshContestsBySeasonYearCommand>
{
    public RefreshContestsBySeasonYearCommandValidator(IDateTimeProvider dateTimeProvider)
    {
        RuleFor(x => x.Sport)
            .IsInEnum()
            .WithMessage("Sport must be a valid enum value");

        RuleFor(x => x.SeasonYear)
            .GreaterThan(2000)
            .WithMessage("Season year must be greater than 2000")
            .Must(year => year <= dateTimeProvider.UtcNow().Year + 1)
            .WithMessage("Season year cannot be more than one year in the future");

        RuleFor(x => x.CorrelationId)
            .NotEmpty()
            .WithMessage("CorrelationId is required");
    }
}
