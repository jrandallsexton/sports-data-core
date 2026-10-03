using FluentValidation;

namespace SportsData.Api.Application.Admin.Commands.RefreshAiExistence;

public class RefreshAiExistenceCommandValidator : AbstractValidator<RefreshAiExistenceCommand>
{
    public RefreshAiExistenceCommandValidator()
    {
        // Week is optional (null = current week), but a supplied one has to be
        // a real week number. Zero and negatives silently match nothing: the
        // backfill filters on the exact value, finds no matchups, inserts no
        // picks and reports success — a typo that looks like a clean run.
        //
        // No upper bound on purpose. Week numbering restarts per phase and
        // postseason weeks vary by sport, so any fixed ceiling would be a guess
        // that eventually rejects a legitimate week. A number past the end of
        // the season still matches nothing, but it is at least an honest zero.
        RuleFor(x => x.Week)
            .GreaterThan(0)
            .When(x => x.Week.HasValue)
            .WithMessage("week must be greater than 0 when supplied; omit it for the current week.");
    }
}
