using FluentValidation;

namespace SportsData.Api.Application.UI.Devices.Commands.RegisterDevice;

public class RegisterDeviceCommandValidator : AbstractValidator<RegisterDeviceCommand>
{
    // Mirror the Notification UserDevices column widths so overlong values fail
    // fast here instead of blowing up the consumer's insert and dead-lettering
    // the UserDeviceRegistered event.
    private const int FcmTokenMaxLength = 256;
    private const int InstallationIdMaxLength = 128;

    private static readonly string[] AllowedPlatforms = { "ios", "android" };

    public RegisterDeviceCommandValidator()
    {
        RuleFor(x => x.InstallationId)
            .NotEmpty().WithMessage("InstallationId is required")
            .MaximumLength(InstallationIdMaxLength)
            .WithMessage($"InstallationId must not exceed {InstallationIdMaxLength} characters.");

        RuleFor(x => x.FcmToken)
            .NotEmpty().WithMessage("FcmToken is required")
            .MaximumLength(FcmTokenMaxLength)
            .WithMessage($"FcmToken must not exceed {FcmTokenMaxLength} characters.");

        RuleFor(x => x.Platform)
            .Must(BeAnAllowedPlatform)
            .WithMessage("Platform must be 'ios' or 'android'");
    }

    private static bool BeAnAllowedPlatform(string? platform)
    {
        if (string.IsNullOrWhiteSpace(platform))
            return false;

        return AllowedPlatforms.Contains(platform.Trim().ToLowerInvariant());
    }
}
