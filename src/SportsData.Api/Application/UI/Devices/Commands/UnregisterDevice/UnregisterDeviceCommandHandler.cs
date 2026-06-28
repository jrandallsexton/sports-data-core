using FluentValidation;

using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Users;

namespace SportsData.Api.Application.UI.Devices.Commands.UnregisterDevice;

public interface IUnregisterDeviceCommandHandler
{
    Task<Result<bool>> ExecuteAsync(
        UnregisterDeviceCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Validates the request and publishes <see cref="UserDeviceUnregistered"/> for
/// the Notification service to drop the device's row. Mirrors
/// <see cref="RegisterDevice.RegisterDeviceCommandHandler"/>: stateless publish
/// with <see cref="DeliveryMode.Direct"/> (no API-side DbContext write to
/// bundle). Notification owns the device store.
/// </summary>
public class UnregisterDeviceCommandHandler : IUnregisterDeviceCommandHandler
{
    private readonly IEventBus _eventBus;
    private readonly IMessageDeliveryScope _deliveryScope;
    private readonly IValidator<UnregisterDeviceCommand> _validator;
    private readonly ILogger<UnregisterDeviceCommandHandler> _logger;

    public UnregisterDeviceCommandHandler(
        IEventBus eventBus,
        IMessageDeliveryScope deliveryScope,
        IValidator<UnregisterDeviceCommand> validator,
        ILogger<UnregisterDeviceCommandHandler> logger)
    {
        _eventBus = eventBus;
        _deliveryScope = deliveryScope;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<bool>> ExecuteAsync(
        UnregisterDeviceCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return new Failure<bool>(default!, ResultStatus.BadRequest, validation.Errors);
        }

        var correlationId = Guid.NewGuid();

        _logger.LogInformation(
            "Publishing UserDeviceUnregistered. UserId={UserId}, CorrelationId={CorrelationId}",
            command.UserId,
            correlationId);

        // Direct delivery — stateless publish, no DbContext write to bundle.
        using (_deliveryScope.Use(DeliveryMode.Direct))
        {
            await _eventBus.Publish(
                new UserDeviceUnregistered(
                    UserId: command.UserId,
                    InstallationId: InstallationIdNormalizer.Normalize(command.InstallationId),
                    CorrelationId: correlationId,
                    CausationId: Guid.NewGuid()),
                cancellationToken);
        }

        return new Success<bool>(true);
    }
}
