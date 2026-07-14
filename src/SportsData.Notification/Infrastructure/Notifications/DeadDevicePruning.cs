using System.Linq;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Notification.Infrastructure.Data;
using SportsData.Notification.Infrastructure.Data.Entities;

namespace SportsData.Notification.Infrastructure.Notifications;

/// <summary>
/// Prunes <see cref="UserDevice"/> rows whose FCM token FCM has rejected as
/// dead/invalid, so they stop failing every send forever. See
/// <c>docs/architecture/notification-dead-token-pruning.md</c>.
/// </summary>
public static class DeadDevicePruning
{
    /// <summary>
    /// True when a push send failed because the FCM token is dead/invalid — the
    /// sender maps <c>Unregistered</c>/<c>InvalidArgument</c> to
    /// <see cref="ResultStatus.NotFound"/> — as opposed to a transient/config
    /// error the device should survive.
    /// </summary>
    public static bool IsDeadTokenFailure(Result<string> sendResult)
        => sendResult is Failure<string> { Status: ResultStatus.NotFound };

    /// <summary>
    /// If <paramref name="sendResult"/> is a dead-token failure, deletes the
    /// device row in its own best-effort save — isolated from the caller's claim
    /// SaveChanges so a stale/missing row (a concurrent prune, or an unregister
    /// between the AsNoTracking read and here) can NOT fail the message. The
    /// device re-registers on next app launch (#508). Returns true when a row
    /// was pruned; false when there was nothing to prune (not a dead token, or
    /// the row was already gone).
    /// </summary>
    public static async Task<bool> MarkDeadDeviceForRemovalAsync(
        this AppDataContext dataContext,
        Result<string> sendResult,
        Guid deviceId,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (!IsDeadTokenFailure(sendResult))
            return false;

        // Send-path callers query devices AsNoTracking, so attach a stub keyed by
        // id. But prefer an already-tracked instance when present (a
        // non-AsNoTracking caller / test) to avoid a duplicate-key tracking clash.
        var device = dataContext.UserDevices.Local.FirstOrDefault(d => d.Id == deviceId)
                     ?? new UserDevice { Id = deviceId };
        dataContext.UserDevices.Remove(device);

        try
        {
            await dataContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Pruned dead push device {DeviceId} — FCM rejected its token; it will re-register on next app launch.",
                deviceId);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // The row was already deleted (a concurrent prune / unregister). This
            // best-effort prune is a no-op then — detach the stale entry so it
            // can't re-fail the caller's terminal SaveChanges, and carry on.
            dataContext.Entry(device).State = EntityState.Detached;
            logger.LogDebug(
                "Dead push device {DeviceId} was already removed; nothing to prune.",
                deviceId);
            return false;
        }
    }
}
