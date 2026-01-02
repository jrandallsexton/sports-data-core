using FluentValidation;

namespace SportsData.Api.Application.Admin.Commands.UpsertMatchupPreview;

public class UpsertMatchupPreviewCommandValidator : AbstractValidator<UpsertMatchupPreviewCommand>
{
    /// <summary>
    /// Initializes a new <see cref="UpsertMatchupPreviewCommandValidator"/> that requires the command's <c>JsonContent</c> to be non-empty.
    /// </summary>
    /// <remarks>
    /// Validation failures for an empty <c>JsonContent</c> produce the message "JSON content cannot be empty".
    /// </remarks>
    public UpsertMatchupPreviewCommandValidator()
    {
        RuleFor(x => x.JsonContent)
            .NotEmpty()
            .WithMessage("JSON content cannot be empty");
    }
}