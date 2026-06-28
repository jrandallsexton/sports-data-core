namespace SportsData.Api.Application.UI.Devices.Commands.UnregisterDevice;

/// <summary>
/// Unregisters a mobile device for the authenticated user (typically at
/// sign-out). UserId is resolved from the JWT in the controller, never trusted
/// from the client, so a caller can only ever unregister against its own
/// identity.
/// </summary>
public class UnregisterDeviceCommand
{
    public Guid UserId { get; set; }

    public required string InstallationId { get; set; }
}
