using FluentValidation;

namespace SportsData.Producer.Application.Contests.Queries.Matchups.GetContestPreviewHistory;

public class GetContestPreviewHistoryQueryValidator : AbstractValidator<GetContestPreviewHistoryQuery>
{
    public GetContestPreviewHistoryQueryValidator()
    {
        RuleFor(x => x.ContestId)
            .NotEqual(Guid.Empty)
            .WithMessage("ContestId must be provided");

        RuleFor(x => x.MeetingCount)
            .InclusiveBetween(1, 25)
            .WithMessage("MeetingCount must be between 1 and 25");

        RuleFor(x => x.RecentGameCount)
            .InclusiveBetween(1, 25)
            .WithMessage("RecentGameCount must be between 1 and 25");
    }
}
