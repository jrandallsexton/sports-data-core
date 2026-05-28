using FluentValidation;

using SportsData.Api.Application.UI.Leagues.Commands.CreateFootballNflLeague.Dtos;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Leagues.Commands.CreateFootballNflLeague;

public class CreateFootballNflLeagueRequestValidator
    : CreateLeagueRequestBaseValidator<CreateFootballNflLeagueRequest>
{
    public CreateFootballNflLeagueRequestValidator(IDateTimeProvider dateTimeProvider)
        : base(dateTimeProvider)
    {
        // Empty DivisionSlugs is allowed (means "include all divisions"); but when the
        // caller provides entries they must be unique. Duplicates almost always indicate
        // a buggy client — the franchise service silently de-dupes them otherwise.
        RuleFor(x => x.DivisionSlugs)
            .Must(HasNoDuplicates)
            .When(x => x.DivisionSlugs is { Count: > 1 })
            .WithMessage("DivisionSlugs contains duplicate entries.");
    }
}
