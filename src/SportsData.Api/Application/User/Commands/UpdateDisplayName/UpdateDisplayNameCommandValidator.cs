using FluentValidation;

namespace SportsData.Api.Application.User.Commands.UpdateDisplayName;

public class UpdateDisplayNameCommandValidator : AbstractValidator<UpdateDisplayNameCommand>
{
    public UpdateDisplayNameCommandValidator()
    {
        RuleFor(x => x.DisplayName)
            .Must(v => !string.IsNullOrWhiteSpace(v))
            .WithMessage("Display name is required.");

        // Free-text label (non-unique). Matches the UpsertUser cap.
        RuleFor(x => x.DisplayName)
            .MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.DisplayName))
            .WithMessage("Display name must not exceed 100 characters.");
    }
}
