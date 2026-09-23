using FluentValidation;

namespace SportsData.Api.Application.Franchises.Seasons.Commands.EnrichFranchiseSeason;

public class EnrichFranchiseSeasonCommandValidator : AbstractValidator<EnrichFranchiseSeasonCommand>
{
    public EnrichFranchiseSeasonCommandValidator()
    {
        RuleFor(x => x.Sport).NotEmpty();
        RuleFor(x => x.League).NotEmpty();
        RuleFor(x => x.FranchiseSlugOrId).NotEmpty();

        // Plausible season label. The lower bound predates every sourced
        // sport; the upper bound stops a typo from asking the Producer to
        // enrich a season that cannot exist yet.
        RuleFor(x => x.SeasonYear)
            .InclusiveBetween(1900, 2100)
            .WithMessage("SeasonYear must be a plausible season label (1900-2100).");
    }
}
