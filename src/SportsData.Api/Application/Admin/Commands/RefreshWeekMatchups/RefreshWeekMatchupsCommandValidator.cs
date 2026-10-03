using FluentValidation;

namespace SportsData.Api.Application.Admin.Commands.RefreshWeekMatchups;

public class RefreshWeekMatchupsCommandValidator : AbstractValidator<RefreshWeekMatchupsCommand>
{
    public RefreshWeekMatchupsCommandValidator()
    {
        // One of two addressing forms, never neither. Ambiguity WITHIN the
        // convenience form is not a validation failure — it depends on what is
        // in the database, so the handler reports the candidates instead.
        RuleFor(x => x)
            .Must(c => c.SeasonWeekId.HasValue || (c.SeasonYear.HasValue && c.SeasonWeek.HasValue))
            .WithName("seasonWeekId")
            .WithMessage("Supply either seasonWeekId, or both seasonYear and seasonWeek.");

        RuleFor(x => x.SeasonWeekId)
            .Must(id => id != Guid.Empty)
            .When(x => x.SeasonWeekId.HasValue)
            .WithMessage("seasonWeekId cannot be an empty guid.");
    }
}
