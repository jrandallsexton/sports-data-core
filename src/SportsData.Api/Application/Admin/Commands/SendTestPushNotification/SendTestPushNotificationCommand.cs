using System.Text.Json.Serialization;

namespace SportsData.Api.Application.Admin.Commands.SendTestPushNotification;

/// <summary>
/// Admin command: send a single test push notification to one device
/// token. Used to prove the FCM pipeline end-to-end during initial setup
/// (Apple Developer APNS key upload + Firebase Console + mobile token
/// roundtrip). NOT a production-shaped notification path — that's the
/// future NotificationDispatcher + Hangfire job described in
/// docs/mobile/push-notifications.md.
/// </summary>
public class SendTestPushNotificationCommand
{
    /// <summary>
    /// FCM device token from the receiving device. Treat as opaque;
    /// validation is delegated to FCM itself.
    /// </summary>
    [JsonPropertyName("token")]
    public required string Token { get; set; }

    /// <summary>
    /// Notification title shown in the system banner. Optional —
    /// defaults to a generic test string when omitted.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// Notification body shown in the system banner. Optional —
    /// defaults to a generic test string when omitted.
    /// </summary>
    [JsonPropertyName("body")]
    public string? Body { get; set; }

    /// <summary>
    /// Optional data payload forwarded into FCM's data block. Mobile
    /// reads this on receive/tap. Useful for testing deep-link routing
    /// during early integration (e.g., { "deepLink": "sportdeets://..." }).
    /// </summary>
    [JsonPropertyName("data")]
    public Dictionary<string, string>? Data { get; set; }
}
