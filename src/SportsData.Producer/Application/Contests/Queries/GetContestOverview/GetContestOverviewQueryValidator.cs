using FluentValidation;

namespace SportsData.Producer.Application.Contests.Queries.GetContestOverview;

public class GetContestOverviewQueryValidator : AbstractValidator<GetContestOverviewQuery>
{
    public GetContestOverviewQueryValidator()
    {
        RuleFor(x => x.ContestId)
            .NotEqual(Guid.Empty)
            .WithMessage("ContestId must be provided");
    }
}
