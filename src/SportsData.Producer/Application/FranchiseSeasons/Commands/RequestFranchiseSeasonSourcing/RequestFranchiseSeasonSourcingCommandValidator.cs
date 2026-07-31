using FluentValidation;

using SportsData.Core.Common;

namespace SportsData.Producer.Application.FranchiseSeasons.Commands.RequestFranchiseSeasonSourcing;

/// <summary>
/// Bounds mirror RefreshContestsBySeasonYearCommandValidator: sourcing a
/// season more than one year out asks ESPN for documents that don't exist
/// yet, and pre-2000 predates the historical sourcing floor.
/// </summary>
public class RequestFranchiseSeasonSourcingCommandValidator
    : AbstractValidator<RequestFranchiseSeasonSourcingCommand>
{
    public RequestFranchiseSeasonSourcingCommandValidator(IDateTimeProvider dateTimeProvider)
    {
        RuleFor(x => x.Sport)
            .IsInEnum()
            .WithMessage("Sport must be a valid enum value");

        RuleFor(x => x.SeasonYear)
            .GreaterThan(2000)
            .WithMessage("Season year must be greater than 2000")
            .Must(year => year <= dateTimeProvider.UtcNow().Year + 1)
            .WithMessage("Season year cannot be more than one year in the future");
    }
}
