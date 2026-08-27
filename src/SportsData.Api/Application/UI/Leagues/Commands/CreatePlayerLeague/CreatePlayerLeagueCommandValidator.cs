using FluentValidation;

using SportsData.Api.Application.Common.Enums;

namespace SportsData.Api.Application.UI.Leagues.Commands.CreatePlayerLeague;

public class CreatePlayerLeagueCommandValidator : AbstractValidator<CreatePlayerLeagueCommand>
{
    private static readonly string[] SupportedSports = ["FootballNcaa", "FootballNfl"];

    public CreatePlayerLeagueCommandValidator()
    {
        RuleFor(x => x.Sport)
            .Must(s => SupportedSports.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Sport must be 'FootballNcaa' or 'FootballNfl'");

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Description)
            .MaximumLength(100); // PickemGroup.Description column limit

        RuleFor(x => x.JoinPolicy)
            // IsDefined too: TryParse happily accepts numeric strings like
            // "999" that no downstream logic handles.
            .Must(p => p is null ||
                       (Enum.TryParse<JoinPolicy>(p, ignoreCase: true, out var parsed) &&
                        Enum.IsDefined(parsed)))
            .WithMessage("Unknown join policy");

        RuleFor(x => x.SeasonYear)
            .InclusiveBetween(2000, 2100)
            .When(x => x.SeasonYear.HasValue);

        RuleFor(x => x.EndsOn)
            .LessThan(new DateTime(2100, 1, 1, 0, 0, 0, DateTimeKind.Utc))
            .When(x => x.EndsOn.HasValue)
            .WithMessage("EndsOn must be before year 2100");

        // Compare NORMALIZED start against normalized end — DateTime
        // comparison ignores Kind, so raw-Local vs effective-UTC would
        // pass or fail by the server's offset.
        RuleFor(x => x.EffectiveStartsOn)
            .LessThanOrEqualTo(x => x.EffectiveEndsOn!.Value)
            .When(x => x.StartsOn.HasValue && x.EndsOn.HasValue &&
                       x.EndsOn.Value < new DateTime(2100, 1, 1, 0, 0, 0, DateTimeKind.Utc))
            .WithMessage("StartsOn must be on or before EndsOn");
    }
}
