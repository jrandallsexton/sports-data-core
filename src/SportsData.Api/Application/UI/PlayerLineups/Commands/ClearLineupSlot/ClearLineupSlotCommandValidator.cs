using FluentValidation;

namespace SportsData.Api.Application.UI.PlayerLineups.Commands.ClearLineupSlot;

public class ClearLineupSlotCommandValidator : AbstractValidator<ClearLineupSlotCommand>
{
    public ClearLineupSlotCommandValidator()
    {
        RuleFor(x => x.SeasonYear).InclusiveBetween(2000, 2100);
        RuleFor(x => x.SeasonWeek).InclusiveBetween(1, 30);
        RuleFor(x => x.SlotId).NotEmpty().MaximumLength(8);
    }
}
