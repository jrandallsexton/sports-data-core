namespace SportsData.Api.Application.UI.Devices.Commands.RegisterDevice;

/// <summary>
/// Registers a mobile device's FCM token for the authenticated user. UserId
/// is resolved from the JWT in the controller, never trusted from the client.
/// </summary>
public class RegisterDeviceCommand
{
    public Guid UserId { get; set; }

    public required string FcmToken { get; set; }

    public required string Platform { get; set; }
}
