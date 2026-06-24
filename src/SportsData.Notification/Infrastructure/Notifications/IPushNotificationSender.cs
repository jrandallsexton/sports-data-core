using SportsData.Core.Common;

namespace SportsData.Notification.Infrastructure.Notifications;

/// <summary>
/// Thin abstraction over FCM (Firebase Cloud Messaging). Lets the
/// Application layer dispatch a single push to one token without
/// taking a direct dependency on FirebaseAdmin.Messaging.
///
/// <para>
/// Intentionally duplicated from <c>SportsData.Api.Infrastructure.Notifications</c>
/// rather than extracted to a shared library — keeps the Notification
/// service self-contained while the API copy is left in place (the API
/// admin test-send endpoint still uses it). Once the Notification
/// service owns all dispatch, the API copy can be retired and the
/// remaining single implementation pulled into Core if it ever needs
/// to be shared again.
/// </para>
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
        IReadOnlyDictionary<string, string> data = null,
        CancellationToken cancellationToken = default);
}
