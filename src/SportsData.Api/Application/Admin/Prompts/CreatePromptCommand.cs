using FluentValidation;

using SportsData.Api.Infrastructure.Data.Entities;
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
        // Bounded by the capture column, not Prompt.Name's own (wider) column:
        // the name is written to MatchupPreviewPrompt.PromptVersion on every
        // generation, so a longer name would create fine and then fail every
        // capture write. Checked trimmed, because the handler stores Name.Trim().
        RuleFor(x => x.Name)
            .NotEmpty()
            .Must(name => name is null || name.Trim().Length <= MatchupPreviewPrompt.PromptVersionMaxLength)
            .WithMessage($"Name must be at most {MatchupPreviewPrompt.PromptVersionMaxLength} characters (it is recorded as PromptVersion on every preview capture).");

        RuleFor(x => x.Text)
            .NotEmpty()
            .WithMessage("Prompt text cannot be empty");

        RuleFor(x => x.Description)
            .MaximumLength(256);
    }
}
