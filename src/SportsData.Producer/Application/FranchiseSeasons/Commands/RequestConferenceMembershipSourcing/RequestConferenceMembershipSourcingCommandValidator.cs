using FluentValidation;

using SportsData.Core.Common;

namespace SportsData.Producer.Application.FranchiseSeasons.Commands.RequestConferenceMembershipSourcing;

/// <summary>
/// Same season-year rules as RequestFranchiseSeasonSourcingCommandValidator:
/// 2000 is the inclusive historical floor, and a season more than one year
/// out has no groups to re-source.
/// </summary>
public class RequestConferenceMembershipSourcingCommandValidator
    : AbstractValidator<RequestConferenceMembershipSourcingCommand>
{
    public RequestConferenceMembershipSourcingCommandValidator(IDateTimeProvider dateTimeProvider)
    {
        RuleFor(x => x.Sport)
            .IsInEnum()
            .WithMessage("Sport must be a valid enum value");

        RuleFor(x => x.SeasonYear)
            .GreaterThanOrEqualTo(2000)
            .WithMessage("Season year must be 2000 or later")
            .Must(year => year <= dateTimeProvider.UtcNow().Year + 1)
            .WithMessage("Season year cannot be more than one year in the future");
    }
}
