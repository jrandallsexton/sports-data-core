using FluentValidation;

namespace SportsData.Producer.Application.Contests.Queries.GetOddsPricingByContestId;

public class GetOddsPricingByContestIdQueryValidator
    : AbstractValidator<GetOddsPricingByContestIdQuery>
{
    public GetOddsPricingByContestIdQueryValidator()
    {
        RuleFor(x => x.ContestId)
            .NotEmpty()
            .WithMessage("contestId is required.");
    }
}
