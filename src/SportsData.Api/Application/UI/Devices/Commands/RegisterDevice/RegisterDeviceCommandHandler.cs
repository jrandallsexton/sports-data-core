using FluentValidation.Results;

using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Users;

namespace SportsData.Api.Application.UI.Devices.Commands.RegisterDevice;

public interface IRegisterDeviceCommandHandler
{
    Task<Result<bool>> ExecuteAsync(
        RegisterDeviceCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolves nothing about identity itself (the controller already mapped the
/// JWT to <see cref="RegisterDeviceCommand.UserId"/>) — it validates the
/// payload and publishes <see cref="UserDeviceRegistered"/> for the
/// Notification service to project into its UserDevices table.
///
/// <para>
/// Stateless publish: no API-side entity write, so we publish with
/// <see cref="DeliveryMode.Direct"/> rather than injecting a DbContext just to
/// flush the outbox. Notification owns the device store; API only resolves the
/// user and announces the registration.
/// </para>
/// </summary>
public class RegisterDeviceCommandHandler : IRegisterDeviceCommandHandler
{
    private static readonly string[] AllowedPlatforms = { "ios", "android" };

    private readonly IEventBus _eventBus;
    private readonly IMessageDeliveryScope _deliveryScope;
    private readonly ILogger<RegisterDeviceCommandHandler> _logger;

    public RegisterDeviceCommandHandler(
        IEventBus eventBus,
        IMessageDeliveryScope deliveryScope,
        ILogger<RegisterDeviceCommandHandler> logger)
    {
        _eventBus = eventBus;
        _deliveryScope = deliveryScope;
        _logger = logger;
    }

    public async Task<Result<bool>> ExecuteAsync(
        RegisterDeviceCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.FcmToken))
        {
            return new Failure<bool>(
                false,
                ResultStatus.BadRequest,
                [new ValidationFailure(nameof(command.FcmToken), "FcmToken is required")]);
        }

        var platform = command.Platform?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(platform) || !AllowedPlatforms.Contains(platform))
        {
            return new Failure<bool>(
                false,
                ResultStatus.BadRequest,
                [new ValidationFailure(nameof(command.Platform), "Platform must be 'ios' or 'android'")]);
        }

        var correlationId = Guid.NewGuid();

        _logger.LogInformation(
            "Publishing UserDeviceRegistered. UserId={UserId}, Platform={Platform}, CorrelationId={CorrelationId}",
            command.UserId,
            platform,
            correlationId);

        // Direct delivery — stateless publish, no DbContext write to bundle.
        using (_deliveryScope.Use(DeliveryMode.Direct))
        {
            await _eventBus.Publish(
                new UserDeviceRegistered(
                    UserId: command.UserId,
                    FcmToken: command.FcmToken.Trim(),
                    Platform: platform,
                    CorrelationId: correlationId,
                    CausationId: Guid.NewGuid()),
                cancellationToken);
        }

        return new Success<bool>(true);
    }
}
