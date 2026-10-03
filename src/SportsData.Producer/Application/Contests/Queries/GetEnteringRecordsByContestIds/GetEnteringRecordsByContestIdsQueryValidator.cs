using FluentValidation;

namespace SportsData.Producer.Application.Contests.Queries.GetEnteringRecordsByContestIds;

public class GetEnteringRecordsByContestIdsQueryValidator
    : AbstractValidator<GetEnteringRecordsByContestIdsQuery>
{
    /// <summary>
    /// Caps a single request. The SQL runs two correlated laterals per contest,
    /// so cost is linear in the batch; 500 measured well under a second against
    /// production-sized data and matches the audit caller's page size.
    /// </summary>
    public const int MaxContestIds = 500;

    public GetEnteringRecordsByContestIdsQueryValidator()
    {
        // An empty array is NOT a validation error — it is a legitimate no-op,
        // and the handler short-circuits it before touching the database.
        RuleFor(x => x.ContestIds)
            .NotNull()
            .WithMessage("contestIds is required.");

        RuleFor(x => x.ContestIds)
            .Must(ids => ids is null || ids.Length <= MaxContestIds)
            .WithMessage($"At most {MaxContestIds} contest ids per request.");

        RuleFor(x => x.ContestIds)
            .Must(ids => ids is null || ids.All(id => id != Guid.Empty))
            .WithMessage("contestIds cannot contain an empty guid.");
    }
}
