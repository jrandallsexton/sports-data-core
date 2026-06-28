using FluentValidation;

namespace SportsData.Api.Application.UI.Devices.Commands.UnregisterDevice;

public class UnregisterDeviceCommandValidator : AbstractValidator<UnregisterDeviceCommand>
{
    private const int InstallationIdMaxLength = 128;

    public UnregisterDeviceCommandValidator()
    {
        RuleFor(x => x.InstallationId)
            .NotEmpty().WithMessage("InstallationId is required")
            .MaximumLength(InstallationIdMaxLength)
            .WithMessage($"InstallationId must not exceed {InstallationIdMaxLength} characters.");
    }
}
