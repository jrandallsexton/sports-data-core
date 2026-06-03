using SportsData.Core.Common;

namespace SportsData.Api.Infrastructure.Notifications;

/// <summary>
/// Thin abstraction over FCM (Firebase Cloud Messaging). Lets the
/// Application layer dispatch a single push to one token without
/// taking a direct dependency on FirebaseAdmin.Messaging.
///
/// v1 surface is single-token send. Multicast batching and the
/// idempotency/dispatch-log work described in
/// docs/mobile/push-notifications.md sit one layer up in the future
/// NotificationDispatcher.
/// </summary>
public interface IPushNotificationSender
{
    /// <summary>
    /// Send a notification to a single device token. Returns the FCM
    /// message ID on success, or a Failure with the upstream error
    /// reason on failure. Caller is responsible for any retry /
    /// token-deactivation logic.
    /// </summary>
    Task<Result<string>> SendAsync(
        string token,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default);
}
