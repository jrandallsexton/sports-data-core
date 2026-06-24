using FluentValidation.Results;

using SportsData.Core.Common;

namespace SportsData.Notification.Infrastructure.Notifications;

/// <summary>
/// Fallback <see cref="IPushNotificationSender"/> registered when
/// <c>CommonConfig:Firebase:ProjectId</c> is not configured (typically local
/// dev / tests). Returns a structured <see cref="Failure{T}"/> on every
/// SendAsync so consumers don't crash trying to resolve
/// <c>FirebaseMessaging.DefaultInstance</c> against a null
/// <c>FirebaseApp.DefaultInstance</c>.
///
/// <para>
/// Side effects land in <c>NotificationLog</c> as <c>Result="Failed_FcmError"</c>
/// rows with <c>FailureReason</c> calling out the missing config — operators
/// see at-a-glance that the cause is configuration, not an actual FCM
/// failure. Easy to grep, easy to ignore in local dev.
/// </para>
/// </summary>
public class NoOpPushNotificationSender : IPushNotificationSender
{
    private readonly ILogger<NoOpPushNotificationSender> _logger;

    public NoOpPushNotificationSender(
        ILogger<NoOpPushNotificationSender> logger)
    {
        _logger = logger;
    }

    public Task<Result<string>> SendAsync(
        string token,
        string title,
        string body,
        IReadOnlyDictionary<string, string> data = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "FCM send no-op'd — Firebase is not configured. Title='{Title}', TokenPrefix={TokenPrefix}",
            title,
            token is { Length: > 8 } ? token[..8] + "…" : "(short)");

        return Task.FromResult<Result<string>>(new Failure<string>(
            string.Empty,
            ResultStatus.Error,
            [new ValidationFailure(
                nameof(token),
                "Firebase not configured (no-op sender registered)")]));
    }
}
