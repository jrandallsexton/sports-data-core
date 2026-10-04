using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.Admin;
using SportsData.Api.Application.Notifications.Commands.SendTestPushNotification;
using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.PickemGroups;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.Notifications;

/// <summary>
/// Operator tools for push notifications and the Notification service's
/// reminder projection. Every action is admin-only: [AdminApiToken] is on
/// the class.
/// </summary>
[ApiController]
[Route("api/notifications")]
[AdminApiToken]
public class NotificationsController : ApiControllerBase
{
    private readonly IEventBus _eventBus;
    private readonly IMessageDeliveryScope _deliveryScope;

    public NotificationsController(
        IEventBus eventBus,
        IMessageDeliveryScope deliveryScope)
    {
        _eventBus = eventBus;
        _deliveryScope = deliveryScope;
    }

    /// <summary>
    /// Send a single test push notification to a specific FCM device
    /// token. Pre-persistence proof-of-concept — see
    /// docs/mobile/push-notifications.md for the production-shaped
    /// dispatcher work that will eventually consume UserDeviceToken
    /// + NotificationDispatchLog. Used to verify the APNS / FCM
    /// pipeline is wired correctly during initial setup.
    ///
    /// Example: POST /api/notifications/test-push
    /// Body: { "token": "...", "title": "...", "body": "...",
    ///         "data": { "deepLink": "sportdeets://..." } }
    /// </summary>
    [HttpPost("test-push")]
    public async Task<ActionResult<SendTestPushNotificationResponse>> SendTestPushNotification(
        [FromBody] SendTestPushNotificationCommand command,
        [FromServices] ISendTestPushNotificationCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(command, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Notification reminder backfill: publishes the (until now
    /// trigger-less) PickemGroupMatchupsRequested event. The API's own
    /// consumer re-publishes PickemGroupMatchupDataPublished for every
    /// FUTURE matchup of the sport; the Notification service upserts its
    /// projection and (re)schedules pick-deadline and contest-start
    /// reminders. Built 2026-09-04: leagues created during the
    /// Notification deploy hold (image pinned at 5371 until mid-Aug)
    /// never had reminders scheduled, and the designed backfill event
    /// had no publisher anywhere. Idempotent — the schedulers no-op on
    /// unchanged fire times.
    /// </summary>
    [HttpPost("matchups/backfill")]
    public async Task<IActionResult> BackfillNotificationMatchups(
        [FromQuery] Sport sport = Sport.FootballNcaa,
        [FromQuery] int? seasonYear = null,
        CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid();

        // No DbContext write on this path — publish straight to the
        // broker. UseBusOutbox would otherwise buffer the event waiting
        // for a SaveChangesAsync that never comes, and the backfill
        // would 202 while silently doing nothing.
        using (_deliveryScope.Use(DeliveryMode.Direct))
        {
            await _eventBus.Publish(new PickemGroupMatchupsRequested(
                sport,
                seasonYear,
                correlationId,
                CausationId.Api.AdminNotificationBackfill), cancellationToken);
        }

        return Accepted(new { correlationId });
    }
}
