using FluentValidation;

namespace SportsData.Producer.Application.Contests.Queries.GetContestPlayLog;

public class GetContestPlayLogQueryValidator : AbstractValidator<GetContestPlayLogQuery>
{
    public GetContestPlayLogQueryValidator()
    {
        RuleFor(x => x.ContestId)
            .NotEqual(Guid.Empty)
            .WithMessage("ContestId must be provided");
    }
}
