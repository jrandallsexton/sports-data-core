using FluentValidation;

using SportsData.Api.Application.Common.Moderation;

namespace SportsData.Api.Application.User.Commands.UpdateDisplayName;

public class UpdateDisplayNameCommandValidator : AbstractValidator<UpdateDisplayNameCommand>
{
    // Short free-text label (non-unique). The DB column allows more, but a
    // display name has no reason to be long; the web/mobile inputs cap to match.
    public const int MaxLength = 25;

    public UpdateDisplayNameCommandValidator()
    {
        RuleFor(x => x.DisplayName)
            .Must(v => !string.IsNullOrWhiteSpace(v))
            .WithMessage("Display name is required.");

        RuleFor(x => x.DisplayName)
            .MaximumLength(MaxLength)
            .When(x => !string.IsNullOrWhiteSpace(x.DisplayName))
            .WithMessage($"Display name must not exceed {MaxLength} characters.");

        // Whole-word profanity check (see ProfanityPolicy — substring matching
        // is deliberately NOT used; real surnames must pass).
        RuleFor(x => x.DisplayName)
            .Must(v => !ProfanityPolicy.ContainsProfanity(v))
            .WithMessage("That display name isn't allowed.");
    }
}
