namespace SportsData.Api.Application.UI.Devices.Dtos;

/// <summary>
/// Mobile-supplied payload for FCM device registration. The user is NOT in
/// the body — it's resolved server-side from the JWT — so a client can only
/// ever register a device against its own authenticated identity.
/// </summary>
public class RegisterDeviceRequest
{
    /// <summary>The FCM registration token from the device install.</summary>
    public required string FcmToken { get; set; }

    /// <summary>"ios" or "android".</summary>
    public required string Platform { get; set; }
}
