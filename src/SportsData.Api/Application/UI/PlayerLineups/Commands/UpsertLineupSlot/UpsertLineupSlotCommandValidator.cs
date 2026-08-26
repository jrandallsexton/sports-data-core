using FluentValidation;

namespace SportsData.Api.Application.UI.PlayerLineups.Commands.UpsertLineupSlot;

/// <summary>
/// Bounds + length gate in front of the upsert. Max lengths mirror the
/// PlayerLineupSlot column configuration — without them an oversized
/// client value reaches SaveChanges and surfaces as a persistence error
/// instead of a 400. Slot membership and position eligibility stay in
/// the handler beside the LineupSlots authority.
/// </summary>
public class UpsertLineupSlotCommandValidator : AbstractValidator<UpsertLineupSlotCommand>
{
    public UpsertLineupSlotCommandValidator()
    {
        RuleFor(x => x.SeasonYear).InclusiveBetween(2000, 2100);
        RuleFor(x => x.SeasonWeek).InclusiveBetween(1, 30);
        RuleFor(x => x.SlotId).NotEmpty().MaximumLength(8);
        RuleFor(x => x.AthleteId).NotEmpty();
        RuleFor(x => x.AthleteSeasonId).NotEmpty();
        RuleFor(x => x.Position).NotEmpty().MaximumLength(4);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.TeamName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.TeamSlug).NotEmpty().MaximumLength(150);
        RuleFor(x => x.OpponentName).MaximumLength(150);
    }
}
