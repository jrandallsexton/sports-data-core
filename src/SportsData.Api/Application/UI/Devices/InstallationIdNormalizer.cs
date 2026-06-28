namespace SportsData.Api.Application.UI.Devices;

/// <summary>
/// Canonicalizes a device <c>InstallationId</c> before it's published, so the
/// register and unregister paths produce the same stable key for the same
/// device. When the value is a GUID (the mobile client mints one via
/// <c>Crypto.randomUUID()</c>), different text representations — uppercase,
/// braces — would otherwise be distinct strings under the unique index and let
/// the same device double-register. Non-GUID values fall through trimmed so the
/// contract stays an opaque string.
/// </summary>
public static class InstallationIdNormalizer
{
    public static string Normalize(string installationId)
    {
        var trimmed = installationId.Trim();
        return Guid.TryParse(trimmed, out var guid) ? guid.ToString() : trimmed;
    }
}
