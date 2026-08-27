using FluentValidation;

using SportsData.Api.Application.Common.Enums;

namespace SportsData.Api.Application.UI.Leagues.Commands.CreatePlayerLeague;

public class CreatePlayerLeagueRequestValidator : AbstractValidator<CreatePlayerLeagueRequest>
{
    private static readonly string[] SupportedSports = ["FootballNcaa", "FootballNfl"];

    public CreatePlayerLeagueRequestValidator()
    {
        RuleFor(x => x.Sport)
            .Must(s => SupportedSports.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Sport must be 'FootballNcaa' or 'FootballNfl'");

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Description)
            .MaximumLength(500);

        RuleFor(x => x.JoinPolicy)
            .Must(p => p is null || Enum.TryParse<JoinPolicy>(p, ignoreCase: true, out _))
            .WithMessage("Unknown join policy");

        RuleFor(x => x.SeasonYear)
            .InclusiveBetween(2000, 2100)
            .When(x => x.SeasonYear.HasValue);

        RuleFor(x => x.StartsOn)
            .LessThanOrEqualTo(x => x.EffectiveEndsOn!.Value)
            .When(x => x.StartsOn.HasValue && x.EndsOn.HasValue)
            .WithMessage("StartsOn must be on or before EndsOn");
    }
}
