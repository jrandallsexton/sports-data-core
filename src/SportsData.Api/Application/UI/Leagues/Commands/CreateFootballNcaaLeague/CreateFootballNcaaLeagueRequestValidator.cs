using FluentValidation;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.UI.Leagues.Commands.CreateFootballNcaaLeague.Dtos;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Leagues.Commands.CreateFootballNcaaLeague;

public class CreateFootballNcaaLeagueRequestValidator
    : CreateLeagueRequestBaseValidator<CreateFootballNcaaLeagueRequest>
{
    public CreateFootballNcaaLeagueRequestValidator(IDateTimeProvider dateTimeProvider)
        : base(dateTimeProvider)
    {
        // RankingFilter is optional — only validate when the caller supplied a value.
        RuleFor(x => x.RankingFilter)
            .Must(IsDefinedEnumName<TeamRankingFilter>)
            .When(x => !string.IsNullOrWhiteSpace(x.RankingFilter))
            .WithMessage(x => $"Invalid ranking filter: {x.RankingFilter}");

        // At least ONE inclusion filter is required. The matchup processor
        // builds the slate from rank hits and conference hits only — a group
        // with neither produces a permanently EMPTY schedule (this rule's
        // earlier comment claimed empty slugs meant "all conferences"; the
        // processor never implemented that, and the operator ruling is
        // filter-required rather than an implicit all. 2026-08-18).
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.RankingFilter)
                       || x.ConferenceSlugs is { Count: > 0 })
            .WithName(nameof(CreateFootballNcaaLeagueRequest.ConferenceSlugs))
            .WithMessage("Choose a ranking filter or at least one conference — a league with neither would have no games.");

        // When the caller provides conference entries they must be unique.
        // Duplicates almost always indicate a buggy client — the franchise
        // service silently de-dupes them otherwise.
        RuleFor(x => x.ConferenceSlugs)
            .Must(HasNoDuplicates)
            .When(x => x.ConferenceSlugs is { Count: > 1 })
            .WithMessage("ConferenceSlugs contains duplicate entries.");
    }
}
