using FluentValidation;

namespace SportsData.Producer.Application.FranchiseSeasons.Commands.RequestSingleFranchiseSeasonSourcing;

public class RequestSingleFranchiseSeasonSourcingCommandValidator
    : AbstractValidator<RequestSingleFranchiseSeasonSourcingCommand>
{
    public RequestSingleFranchiseSeasonSourcingCommandValidator()
    {
        RuleFor(x => x.FranchiseSeasonId)
            .NotEmpty()
            .WithMessage("FranchiseSeasonId is required.");

        RuleFor(x => x.CorrelationId)
            .NotEmpty()
            .WithMessage("CorrelationId is required; the controller resolves it from X-Correlation-Id or the current activity.");
    }
}
