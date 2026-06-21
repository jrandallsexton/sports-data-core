using FluentValidation;

namespace SportsData.Producer.Application.Contests.Commands;

public class ReenrichContestCommandValidator : AbstractValidator<ReenrichContestCommand>
{
    public ReenrichContestCommandValidator()
    {
        RuleFor(x => x.ContestId)
            .NotEmpty()
            .WithMessage("ContestId is required");

        RuleFor(x => x.CorrelationId)
            .NotEmpty()
            .WithMessage("CorrelationId is required");
    }
}
