using System.Linq;

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
    /// If <paramref name="sendResult"/> is a dead-token failure, marks the device
    /// row for deletion — a tracked <c>Remove</c> with no immediate save, so it
    /// flushes with the caller's terminal <c>SaveChangesAsync</c> (the one that
    /// writes the claim outcome) in a single transaction. The device re-registers
    /// on next app launch (#508). Returns true when it marked a row.
    /// </summary>
    public static bool MarkDeadDeviceForRemoval(
        this AppDataContext dataContext,
        Result<string> sendResult,
        Guid deviceId,
        ILogger logger)
    {
        if (!IsDeadTokenFailure(sendResult))
            return false;

        // Send-path callers query devices AsNoTracking, so attach a stub keyed by
        // id. But prefer an already-tracked instance when present (a
        // non-AsNoTracking caller / test) to avoid a duplicate-key tracking clash.
        var device = dataContext.UserDevices.Local.FirstOrDefault(d => d.Id == deviceId)
                     ?? new UserDevice { Id = deviceId };
        dataContext.UserDevices.Remove(device);

        logger.LogInformation(
            "Pruning dead push device {DeviceId} — FCM rejected its token; it will re-register on next app launch.",
            deviceId);

        return true;
    }
}
