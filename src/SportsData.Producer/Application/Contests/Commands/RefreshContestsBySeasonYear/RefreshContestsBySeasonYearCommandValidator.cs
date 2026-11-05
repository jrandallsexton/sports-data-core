using FluentValidation;

using SportsData.Core.Common;
using SportsData.Producer.Application.Contests.Commands.UpdateContest;

namespace SportsData.Producer.Application.Contests.Commands.RefreshContestsBySeasonYear;

public class RefreshContestsBySeasonYearCommandValidator : AbstractValidator<RefreshContestsBySeasonYearCommand>
{
    public RefreshContestsBySeasonYearCommandValidator(IDateTimeProvider dateTimeProvider)
    {
        RuleFor(x => x.Sport)
            .IsInEnum()
            .WithMessage("Sport must be a valid enum value");

        RuleFor(x => x.SeasonYear)
            // Inclusive: 2000 is a sourced season.
            .GreaterThanOrEqualTo(2000)
            .WithMessage("Season year must be 2000 or later")
            .Must(year => year <= dateTimeProvider.UtcNow().Year + 1)
            .WithMessage("Season year cannot be more than one year in the future");

        RuleFor(x => x.CorrelationId)
            .NotEmpty()
            .WithMessage("CorrelationId is required");

        // Narrowing only: a season-wide refresh must never widen past the
        // standard refresh set (rosters/athletes are what buried the
        // pipeline on 2026-08-29). Empty would mean "Event documents only",
        // which re-sources nothing useful.
        When(x => x.IncludeLinkedDocumentTypes is not null, () =>
        {
            RuleFor(x => x.IncludeLinkedDocumentTypes!)
                .NotEmpty()
                .WithMessage("IncludeLinkedDocumentTypes, when provided, must not be empty");

            RuleForEach(x => x.IncludeLinkedDocumentTypes)
                .Must(type => UpdateContestCommand.DefaultIncludeLinkedDocumentTypes.Contains(type))
                .WithMessage((_, type) =>
                    $"IncludeLinkedDocumentTypes may only narrow the standard refresh set; {type} is not in it");
        });
    }
}
