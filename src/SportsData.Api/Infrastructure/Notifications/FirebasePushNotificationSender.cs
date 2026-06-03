using FirebaseAdmin.Messaging;

using FluentValidation.Results;

using SportsData.Core.Common;

namespace SportsData.Api.Infrastructure.Notifications;

/// <summary>
/// Firebase Cloud Messaging implementation of <see cref="IPushNotificationSender"/>.
/// Relies on the global <see cref="FirebaseAdmin.FirebaseApp.DefaultInstance"/>
/// initialized in Program.cs (same instance used for ID-token verification).
///
/// iOS APNS relay is handled by FCM automatically once the APNS Auth Key
/// (.p8) is uploaded to the Firebase project — this code stays platform-
/// agnostic. The Platform column the dispatch log will eventually carry
/// is for diagnostics, not for branching the send path.
/// </summary>
public class FirebasePushNotificationSender : IPushNotificationSender
{
    private readonly ILogger<FirebasePushNotificationSender> _logger;

    public FirebasePushNotificationSender(
        ILogger<FirebasePushNotificationSender> logger)
    {
        _logger = logger;
    }

    public async Task<Result<string>> SendAsync(
        string token,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new Failure<string>(
                string.Empty,
                ResultStatus.BadRequest,
                [new ValidationFailure(nameof(token), "FCM token is required")]);
        }

        var message = new Message
        {
            Token = token,
            Notification = new Notification
            {
                Title = title,
                Body = body
            },
            Data = data,
        };

        try
        {
            var messageId = await FirebaseMessaging.DefaultInstance.SendAsync(
                message,
                cancellationToken);

            _logger.LogInformation(
                "FCM send succeeded. MessageId={MessageId}, TokenPrefix={TokenPrefix}",
                messageId,
                SafeTokenPrefix(token));

            return new Success<string>(messageId);
        }
        catch (FirebaseMessagingException ex)
        {
            // FCM surfaces structured error codes (Unregistered = token dead,
            // InvalidArgument = malformed, SenderIdMismatch = wrong project,
            // QuotaExceeded = rate-limited, Unavailable = service issue).
            // For v1 we just bubble the code + message; future token-
            // deactivation logic will branch on ex.MessagingErrorCode here.
            _logger.LogWarning(
                ex,
                "FCM send failed. ErrorCode={ErrorCode}, MessagingErrorCode={MessagingErrorCode}, TokenPrefix={TokenPrefix}",
                ex.ErrorCode,
                ex.MessagingErrorCode,
                SafeTokenPrefix(token));

            return new Failure<string>(
                string.Empty,
                ResultStatus.Error,
                [new ValidationFailure(
                    nameof(token),
                    $"FCM {ex.MessagingErrorCode}: {ex.Message}")]);
        }
    }

    // Don't log full FCM tokens — they're effectively credentials for
    // pushing to that device. Prefix-only gives enough to correlate with
    // a specific device when investigating without exposing the token.
    private static string SafeTokenPrefix(string token)
        => token.Length > 8 ? token[..8] + "…" : "(short)";
}
