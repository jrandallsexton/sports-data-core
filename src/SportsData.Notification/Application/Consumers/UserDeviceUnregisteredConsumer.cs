using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Eventing.Events.Users;
using SportsData.Notification.Infrastructure.Data;

namespace SportsData.Notification.Application.Consumers
{
    /// <summary>
    /// Removes a device's <see cref="Infrastructure.Data.Entities.UserDevice"/>
    /// row when the mobile client unregisters (typically at sign-out), so the
    /// signed-out user's pushes stop reaching that device. Counterpart to
    /// <see cref="UserDeviceRegisteredConsumer"/>.
    ///
    /// <para>
    /// Idempotent: deletion is scoped to <c>(InstallationId, UserId)</c> so a
    /// user only removes their own current ownership of the device — if a
    /// different account has already claimed the install, this is a no-op and
    /// leaves the new owner's row intact. A missing row is also a no-op
    /// (at-least-once redelivery, or a device that never registered).
    /// </para>
    /// </summary>
    public class UserDeviceUnregisteredConsumer : IConsumer<UserDeviceUnregistered>
    {
        private readonly ILogger<UserDeviceUnregisteredConsumer> _logger;
        private readonly AppDataContext _dataContext;

        public UserDeviceUnregisteredConsumer(
            ILogger<UserDeviceUnregisteredConsumer> logger,
            AppDataContext dataContext)
        {
            _logger = logger;
            _dataContext = dataContext;
        }

        public async Task Consume(ConsumeContext<UserDeviceUnregistered> context)
        {
            var msg = context.Message;
            using var _ = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = msg.CorrelationId,
                ["UserId"] = msg.UserId
            });

            var existing = await _dataContext.UserDevices
                .FirstOrDefaultAsync(
                    d => d.InstallationId == msg.InstallationId && d.UserId == msg.UserId,
                    context.CancellationToken);

            if (existing is null)
            {
                _logger.LogInformation("UserDevice unregister no-op (no matching row). UserId={UserId}", msg.UserId);
                return;
            }

            _dataContext.UserDevices.Remove(existing);
            await _dataContext.SaveChangesAsync(context.CancellationToken);
            _logger.LogInformation("UserDevice unregistered. UserId={UserId}, DeviceId={DeviceId}", msg.UserId, existing.Id);
        }
    }
}
