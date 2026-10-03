using FluentValidation;

namespace SportsData.Api.Application.Franchises.Seasons.Commands.SourceFranchiseSeason;

public class SourceFranchiseSeasonCommandValidator : AbstractValidator<SourceFranchiseSeasonCommand>
{
    public SourceFranchiseSeasonCommandValidator()
    {
        RuleFor(x => x.Sport).NotEmpty();
        RuleFor(x => x.League).NotEmpty();
        RuleFor(x => x.FranchiseSlugOrId).NotEmpty();

        // Same plausible-season bounds as EnrichFranchiseSeasonCommandValidator.
        RuleFor(x => x.SeasonYear)
            .InclusiveBetween(1900, 2100)
            .WithMessage("SeasonYear must be a plausible season label (1900-2100).");
    }
}
