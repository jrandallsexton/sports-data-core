using FluentValidation;

using SportsData.Core.Common;

namespace SportsData.Producer.Application.Athletes.Commands.RequestAthleteSeasonStatisticsSourcing;

/// <summary>
/// Same season-year bounds as the sibling franchise-season sourcing
/// validator (2000 floor inclusive; at most one year out). SeasonType is
/// restricted to the two scopes ESPN publishes athlete statistics under:
/// 2 (regular season) and 3 (through postseason).
/// </summary>
public class RequestAthleteSeasonStatisticsSourcingCommandValidator
    : AbstractValidator<RequestAthleteSeasonStatisticsSourcingCommand>
{
    public RequestAthleteSeasonStatisticsSourcingCommandValidator(IDateTimeProvider dateTimeProvider)
    {
        RuleFor(x => x.Sport)
            .IsInEnum()
            .WithMessage("Sport must be a valid enum value");

        RuleFor(x => x.SeasonYear)
            .GreaterThanOrEqualTo(2000)
            .WithMessage("Season year must be 2000 or later")
            .Must(year => year <= dateTimeProvider.UtcNow().Year + 1)
            .WithMessage("Season year cannot be more than one year in the future");

        RuleFor(x => x.SeasonType)
            .InclusiveBetween(2, 3)
            .WithMessage("SeasonType must be 2 (regular season) or 3 (through postseason)");
    }
}
