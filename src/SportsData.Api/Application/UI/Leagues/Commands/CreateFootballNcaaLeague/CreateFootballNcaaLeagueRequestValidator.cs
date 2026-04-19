using FluentValidation;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.UI.Leagues.Commands.CreateFootballNcaaLeague.Dtos;

namespace SportsData.Api.Application.UI.Leagues.Commands.CreateFootballNcaaLeague;

public class CreateFootballNcaaLeagueRequestValidator
    : CreateLeagueRequestBaseValidator<CreateFootballNcaaLeagueRequest>
{
    public CreateFootballNcaaLeagueRequestValidator()
    {
        // RankingFilter is optional — only validate when the caller supplied a value.
        RuleFor(x => x.RankingFilter)
            .Must(IsDefinedEnumName<TeamRankingFilter>)
            .When(x => !string.IsNullOrWhiteSpace(x.RankingFilter))
            .WithMessage(x => $"Invalid ranking filter: {x.RankingFilter}");

        // Empty ConferenceSlugs is allowed (means "include all conferences"); but when
        // the caller provides entries they must be unique. Duplicates almost always
        // indicate a buggy client — the franchise service silently de-dupes them otherwise.
        RuleFor(x => x.ConferenceSlugs)
            .Must(HasNoDuplicates)
            .When(x => x.ConferenceSlugs is { Count: > 1 })
            .WithMessage("ConferenceSlugs contains duplicate entries.");
    }
}
