using FluentValidation;

using SportsData.Api.Application.UI.Leagues.Commands.CreateBaseballMlbLeague.Dtos;

namespace SportsData.Api.Application.UI.Leagues.Commands.CreateBaseballMlbLeague;

public class CreateBaseballMlbLeagueRequestValidator
    : CreateLeagueRequestBaseValidator<CreateBaseballMlbLeagueRequest>
{
    public CreateBaseballMlbLeagueRequestValidator()
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
