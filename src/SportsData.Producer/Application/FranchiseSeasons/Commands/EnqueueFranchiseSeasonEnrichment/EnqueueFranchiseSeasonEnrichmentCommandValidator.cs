using FluentValidation;

namespace SportsData.Producer.Application.FranchiseSeasons.Commands.EnqueueFranchiseSeasonEnrichment;

public class EnqueueFranchiseSeasonEnrichmentCommandValidator : AbstractValidator<EnqueueFranchiseSeasonEnrichmentCommand>
{
    public EnqueueFranchiseSeasonEnrichmentCommandValidator()
    {
        RuleFor(x => x.Sport)
            .IsInEnum()
            .WithMessage("Sport must be a valid enum value");

        RuleFor(x => x.SeasonYear)
            .GreaterThan(2000)
            .WithMessage("Season year must be greater than 2000")
            .Must(year => year <= DateTime.UtcNow.Year + 1)
            .WithMessage("Season year cannot be more than one year in the future");
    }
}
