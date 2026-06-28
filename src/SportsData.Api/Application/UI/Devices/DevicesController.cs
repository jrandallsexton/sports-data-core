using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.UI.Devices.Commands.RegisterDevice;
using SportsData.Api.Application.UI.Devices.Commands.UnregisterDevice;
using SportsData.Api.Application.UI.Devices.Dtos;
using SportsData.Api.Extensions;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.UI.Devices;

[ApiController]
[Route("ui/devices")]
public class DevicesController : ApiControllerBase
{
    /// <summary>
    /// Registers (or refreshes) the calling device's FCM token for the
    /// authenticated user. Idempotent downstream: the Notification consumer
    /// upserts on (UserId, FcmToken), so the mobile client can safely call
    /// this on every launch / token refresh.
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<bool>> RegisterDevice(
        [FromBody] RegisterDeviceRequest request,
        [FromServices] IRegisterDeviceCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetCurrentUserId();

        var command = new RegisterDeviceCommand
        {
            UserId = userId,
            InstallationId = request.InstallationId,
            FcmToken = request.FcmToken,
            Platform = request.Platform
        };

        var result = await handler.ExecuteAsync(command, cancellationToken);

        if (result.IsSuccess)
            return NoContent();

        return result.ToActionResult();
    }

    /// <summary>
    /// Unregisters the calling device (by its stable installation id) for the
    /// authenticated user — typically at sign-out — so the Notification service
    /// drops the device's row and stops sending this user's pushes to it.
    /// Scoped to the JWT user, so a caller can only unregister its own device.
    /// </summary>
    [HttpDelete("{installationId}")]
    [Authorize]
    public async Task<ActionResult<bool>> UnregisterDevice(
        string installationId,
        [FromServices] IUnregisterDeviceCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetCurrentUserId();

        var command = new UnregisterDeviceCommand
        {
            UserId = userId,
            InstallationId = installationId
        };

        var result = await handler.ExecuteAsync(command, cancellationToken);

        if (result.IsSuccess)
            return NoContent();

        return result.ToActionResult();
    }
}
