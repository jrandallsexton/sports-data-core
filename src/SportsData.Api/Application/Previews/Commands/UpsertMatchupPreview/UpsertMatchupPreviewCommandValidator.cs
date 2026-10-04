using FluentValidation;

namespace SportsData.Api.Application.Previews.Commands.UpsertMatchupPreview;

public class UpsertMatchupPreviewCommandValidator : AbstractValidator<UpsertMatchupPreviewCommand>
{
    public UpsertMatchupPreviewCommandValidator()
    {
        RuleFor(x => x.JsonContent)
            .NotEmpty()
            .WithMessage("JSON content cannot be empty");
    }
}
