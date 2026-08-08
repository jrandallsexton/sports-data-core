using FluentValidation;

using SportsData.Core.Common;

namespace SportsData.Api.Application.Admin.Prompts;

public class CreatePromptCommand
{
    /// <summary>Human-readable version name (unique) — becomes PromptVersion on captures.</summary>
    public required string Name { get; set; }

    /// <summary>Null = applies to any sport.</summary>
    public Sport? Sport { get; set; }

    public bool WithStats { get; set; }

    /// <summary>Make this the active prompt for its (Sport, WithStats) slot; clears the previous default.</summary>
    public bool IsDefault { get; set; }

    public string? Description { get; set; }

    public required string Text { get; set; }
}

public class CreatePromptCommandValidator : AbstractValidator<CreatePromptCommand>
{
    public CreatePromptCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Text)
            .NotEmpty()
            .WithMessage("Prompt text cannot be empty");

        RuleFor(x => x.Description)
            .MaximumLength(256);
    }
}
