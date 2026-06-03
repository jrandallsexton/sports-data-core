using System.Text.Json.Serialization;

namespace SportsData.Api.Application.Admin.Commands.SendTestPushNotification;

/// <summary>
/// Response from a test push notification send. The FCM-returned
/// MessageId is the primary "it worked" signal — present means FCM
/// accepted the message for delivery. Whether the device actually got
/// it is observable on the device, not here.
/// </summary>
public class SendTestPushNotificationResponse
{
    /// <summary>
    /// FCM message ID. Format is typically `projects/{project}/messages/{id}`.
    /// </summary>
    [JsonPropertyName("messageId")]
    public required string MessageId { get; set; }

    /// <summary>
    /// UTC timestamp the send was issued at (server clock). For
    /// correlating with mobile-side received-at timestamps when
    /// debugging delivery latency.
    /// </summary>
    [JsonPropertyName("sentUtc")]
    public DateTime SentUtc { get; set; }

    public static SendTestPushNotificationResponse Empty() => new()
    {
        MessageId = string.Empty,
        SentUtc = default
    };
}
