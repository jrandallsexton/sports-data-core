using FluentValidation;

namespace SportsData.Api.Application.User.Commands.UpdateUsername;

public class UpdateUsernameCommandValidator : AbstractValidator<UpdateUsernameCommand>
{
    public UpdateUsernameCommandValidator()
    {
        RuleFor(x => x.Username)
            .Must(UsernameNormalizer.IsValid)
            .WithMessage(
                $"Username must be {UsernameNormalizer.MinLength}–{UsernameNormalizer.MaxLength} characters, "
                + "use only letters, numbers, or underscores, and not be reserved.");
    }
}
