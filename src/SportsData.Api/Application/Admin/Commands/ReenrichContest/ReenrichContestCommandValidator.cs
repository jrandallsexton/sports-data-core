using FluentValidation;

namespace SportsData.Api.Application.Admin.Commands.ReenrichContest;

public class ReenrichContestCommandValidator : AbstractValidator<ReenrichContestCommand>
{
    public ReenrichContestCommandValidator()
    {
        RuleFor(x => x.ContestId)
            .NotEmpty()
            .WithMessage("ContestId is required");

        RuleFor(x => x.Sport)
            .IsInEnum()
            .WithMessage("Sport must be a valid value");
    }
}
