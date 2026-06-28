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
    /// Idempotent: the load filter is scoped to <c>(InstallationId, UserId)</c>
    /// so a user only removes their own current ownership of the device — if a
    /// different account has already claimed the install (the row's owner is no
    /// longer this user), the load returns null and we no-op, leaving the new
    /// owner's row intact. A missing row is also a no-op (at-least-once
    /// redelivery, or a device that never registered).
    /// </para>
    ///
    /// <para>
    /// Accepted race: there is a narrow load-then-delete window. If a
    /// registration reassigns this install to a new owner <em>after</em> our
    /// load but <em>before</em> SaveChanges, the tracked delete (by primary key)
    /// would remove the reassigned row. This is left unguarded deliberately: the
    /// window requires a sub-second interleave of an unregister and a register
    /// for the same install, and it is self-healing — the mobile client
    /// re-registers the device on its next launch / auth change, restoring the
    /// row. An atomic guard was considered (ExecuteDelete needs a relational test
    /// provider whose only option pulls an unpatched high-severity native dep;
    /// xmin concurrency is no longer cleanly supported in Npgsql 10) and judged
    /// not worth the cost for a rare, self-correcting edge. See PR #477 / #475.
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
