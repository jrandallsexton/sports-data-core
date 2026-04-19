using FluentValidation;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.UI.Leagues.Commands.CreateFootballNcaaLeague.Dtos;

namespace SportsData.Api.Application.UI.Leagues.Commands.CreateFootballNcaaLeague;

public class CreateFootballNcaaLeagueRequestValidator : AbstractValidator<CreateFootballNcaaLeagueRequest>
{
    public CreateFootballNcaaLeagueRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("League name is required.");

        RuleFor(x => x.PickType)
            .Must(IsDefinedEnumName<PickType>)
            .WithMessage(x => $"Invalid pick type: {x.PickType}");

        RuleFor(x => x.TiebreakerType)
            .Must(IsDefinedEnumName<TiebreakerType>)
            .WithMessage(x => $"Invalid tiebreaker type: {x.TiebreakerType}");

        RuleFor(x => x.TiebreakerTiePolicy)
            .Must(IsDefinedEnumName<TiebreakerTiePolicy>)
            .WithMessage(x => $"Invalid tiebreaker tie policy: {x.TiebreakerTiePolicy}");

        // RankingFilter is optional — only validate when the caller supplied a value.
        RuleFor(x => x.RankingFilter)
            .Must(IsDefinedEnumName<TeamRankingFilter>)
            .When(x => !string.IsNullOrWhiteSpace(x.RankingFilter))
            .WithMessage(x => $"Invalid ranking filter: {x.RankingFilter}");

        RuleFor(x => x)
            .Must(x => !x.StartsOn.HasValue || !x.EndsOn.HasValue || x.StartsOn.Value < x.EndsOn.Value)
            .WithName(nameof(CreateFootballNcaaLeagueRequest.EndsOn))
            .WithMessage("EndsOn must be after StartsOn.");

        // Empty ConferenceSlugs is allowed (means "include all conferences"); but when
        // the caller provides entries they must be unique. Duplicates almost always
        // indicate a buggy client — the franchise service silently de-dupes them otherwise.
        RuleFor(x => x.ConferenceSlugs)
            .Must(HasNoDuplicates)
            .When(x => x.ConferenceSlugs is { Count: > 1 })
            .WithMessage("ConferenceSlugs contains duplicate entries.");
    }

    private static bool HasNoDuplicates(IEnumerable<string> values) =>
        values is null
        || values.Count() == values.Distinct(StringComparer.OrdinalIgnoreCase).Count();

    /// <summary>
    /// Ensures the string parses to the enum *and* the resulting value is one of the
    /// declared members. Plain <c>Enum.TryParse</c> accepts any numeric string (e.g. "999"),
    /// which <c>Enum.IsDefined</c> filters out.
    /// </summary>
    private static bool IsDefinedEnumName<TEnum>(string? value) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed)
            && Enum.IsDefined(typeof(TEnum), parsed);
    }
}
