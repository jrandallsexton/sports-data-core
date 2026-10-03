using FluentValidation;

namespace SportsData.Producer.Application.FranchiseSeasons.Commands.EnqueueSingleFranchiseSeasonEnrichment;

public class EnqueueSingleFranchiseSeasonEnrichmentCommandValidator
    : AbstractValidator<EnqueueSingleFranchiseSeasonEnrichmentCommand>
{
    public EnqueueSingleFranchiseSeasonEnrichmentCommandValidator()
    {
        RuleFor(x => x.FranchiseSeasonId)
            .NotEmpty()
            .WithMessage("FranchiseSeasonId is required.");

        RuleFor(x => x.CorrelationId)
            .NotEmpty()
            .WithMessage("CorrelationId is required; the controller resolves it from X-Correlation-Id or the current activity.");
    }
}
