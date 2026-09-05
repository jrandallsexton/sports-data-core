#nullable enable

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Notification.Infrastructure.Data;
using SportsData.Notification.Infrastructure.Notifications;

namespace SportsData.Notification.Application.Reminders;

public enum PushFanoutResult
{
    /// <summary>User has no notification-enabled devices; nothing attempted.</summary>
    NoDevices,

    /// <summary>At least one device accepted the push.</summary>
    Sent,

    /// <summary>Every device attempt failed; see <see cref="PushFanoutOutcome.FailureReason"/>.</summary>
    Failed
}

public record PushFanoutOutcome(PushFanoutResult Result, string? FailureReason);

public interface IPushDeviceFanout
{
    Task<PushFanoutOutcome> SendToUserDevicesAsync(Guid userId, string title, string body);
}

/// <summary>
/// Per-device FCM fan-out shared by the reminder handlers: resolves the
/// user's notification-enabled devices, sends to each, prunes dead tokens,
/// and aggregates the outcome (any success → Sent; all failures → Failed).
///
/// <para>
/// Per-device try/catch: an unhandled exception from SendAsync (e.g.
/// network failure, TaskCanceledException, anything outside
/// FirebaseMessagingException which FirebasePushNotificationSender already
/// maps to Failure&lt;string&gt;) would otherwise escape before the
/// caller's claim finalization. The claim row would sit at "Dispatching"
/// permanently — Hangfire retries collide on the unique-constraint dedupe
/// path and short-circuit. Catching here lets one failing device fail
/// loudly in the audit log without blocking the remaining devices or the
/// caller's terminal save.
/// </para>
/// </summary>
public class PushDeviceFanout : IPushDeviceFanout
{
    private const int FailureReasonMaxLength = 512;
    private const string FailureReasonTruncationSuffix = "…(truncated)";

    private readonly ILogger<PushDeviceFanout> _logger;
    private readonly AppDataContext _dataContext;
    private readonly IPushNotificationSender _pushSender;

    public PushDeviceFanout(
        ILogger<PushDeviceFanout> logger,
        AppDataContext dataContext,
        IPushNotificationSender pushSender)
    {
        _logger = logger;
        _dataContext = dataContext;
        _pushSender = pushSender;
    }

    public async Task<PushFanoutOutcome> SendToUserDevicesAsync(Guid userId, string title, string body)
    {
        var devices = await _dataContext.UserDevices
            .AsNoTracking()
            .Where(d => d.UserId == userId && d.NotificationsEnabled)
            .Select(d => new { d.Id, d.FcmToken, d.Platform })
            .ToListAsync();

        if (devices.Count == 0)
        {
            return new PushFanoutOutcome(PushFanoutResult.NoDevices, null);
        }

        var successCount = 0;
        var failureReasons = new List<string>();
        foreach (var device in devices)
        {
            try
            {
                var result = await _pushSender.SendAsync(device.FcmToken, title, body);
                if (result is Success<string>)
                {
                    successCount++;
                }
                else if (result is Failure<string> failure)
                {
                    var reason = failure.Errors.FirstOrDefault()?.ErrorMessage ?? "unknown";
                    failureReasons.Add($"{device.Platform}:{reason}");
                    // Dead token → prune the device (isolated best-effort save).
                    await _dataContext.MarkDeadDeviceForRemovalAsync(result, device.Id, _logger);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Unexpected exception sending FCM to DeviceId {DeviceId}.",
                    device.Id);
                failureReasons.Add($"{device.Platform}:exception:{ex.GetType().Name}");
            }
        }

        return new PushFanoutOutcome(
            successCount > 0 ? PushFanoutResult.Sent : PushFanoutResult.Failed,
            ComposeFailureReason(failureReasons));
    }

    private static string? ComposeFailureReason(List<string> failureReasons)
    {
        if (failureReasons.Count == 0)
            return null;

        var joined = string.Join("; ", failureReasons);
        if (joined.Length <= FailureReasonMaxLength)
            return joined;

        var cutoff = FailureReasonMaxLength - FailureReasonTruncationSuffix.Length;
        return joined.Substring(0, cutoff) + FailureReasonTruncationSuffix;
    }
}
