#nullable enable

using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Extensions;
using SportsData.Notification.Application.Backfill.Commands.RequestPickemGroupMatchupsBackfill;
using SportsData.Notification.Application.Backfill.Commands.RequestPickemGroupsBackfill;
using SportsData.Notification.Application.Backfill.Commands.RequestUsersBackfill;
using SportsData.Notification.Infrastructure.Auth;

namespace SportsData.Notification.Application.Backfill;

/// <summary>
/// Operator-triggered endpoints that emit backfill request events. Each
/// endpoint is a thin shim over its command handler: publish the request
/// event, return 202. The actual per-entity fan-out happens on the API side
/// (it owns the source data), and the per-entity data events arrive back
/// here on Notification's own consumers.
///
/// <para>
/// Protected by <see cref="ApiKeyAuthAttribute"/>. Not part of the
/// user-facing surface — Notification's regular routes (device
/// registration, preferences) authenticate via JWT like the rest of
/// the platform; the API-key gate is just for these admin operations.
/// </para>
/// </summary>
[ApiController]
[Route("admin/backfill")]
[ApiKeyAuth]
public class BackfillController : ControllerBase
{
    [HttpPost("users")]
    public async Task<ActionResult> RequestUsers(
        [FromServices] IRequestUsersBackfillCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(cancellationToken);
        return result.ToActionResult(correlationId => new AcceptedResult((string?)null, new { correlationId }));
    }

    [HttpPost("pickemgroups")]
    public async Task<ActionResult> RequestPickemGroups(
        [FromServices] IRequestPickemGroupsBackfillCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(cancellationToken);
        return result.ToActionResult(correlationId => new AcceptedResult((string?)null, new { correlationId }));
    }

    [HttpPost("pickemgroupmatchups")]
    public async Task<ActionResult> RequestPickemGroupMatchups(
        [FromServices] IRequestPickemGroupMatchupsBackfillCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(cancellationToken);
        return result.ToActionResult(correlationId => new AcceptedResult((string?)null, new { correlationId }));
    }
}
