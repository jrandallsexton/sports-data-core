using FluentValidation;

namespace SportsData.Producer.Application.Contests.Queries.GetGameDates;

public class GetGameDatesQueryValidator : AbstractValidator<GetGameDatesQuery>
{
    public GetGameDatesQueryValidator()
    {
        // Both bounds are optional — open-ended (and fully-open) ranges are valid
        // by design; the SQL is null-tolerant. The only invariant worth enforcing
        // is that an EXPLICIT window can't be inverted.
        RuleFor(x => x)
            .Must(q => !q.FromUtc.HasValue || !q.ToUtc.HasValue || q.FromUtc.Value <= q.ToUtc.Value)
            .WithName("from")
            .WithMessage("'from' must be on or before 'to'.");
    }
}
