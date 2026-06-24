using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Users;
using SportsData.Notification.Infrastructure.Auth;

namespace SportsData.Notification.Controllers
{
    /// <summary>
    /// Operator-triggered endpoints that emit backfill request events. Each
    /// endpoint is a thin shim: validate, publish the request event, return
    /// 202. The actual per-entity fan-out happens on the API side (it owns
    /// the source data), and the per-entity data events arrive back here on
    /// Notification's own consumers.
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
        private readonly ILogger<BackfillController> _logger;
        private readonly IEventBus _eventBus;
        private readonly IMessageDeliveryScope _deliveryScope;

        public BackfillController(
            ILogger<BackfillController> logger,
            IEventBus eventBus,
            IMessageDeliveryScope deliveryScope)
        {
            _logger = logger;
            _eventBus = eventBus;
            _deliveryScope = deliveryScope;
        }

        /// <summary>
        /// Triggers a full backfill of the local <c>User</c> projection by
        /// publishing <see cref="UsersRequested"/>. The API consumer responds
        /// with one <c>UserDataPublished</c> per user, and Notification's
        /// own <c>UserDataPublishedConsumer</c> upserts them locally.
        /// </summary>
        [HttpPost("users")]
        public async Task<IActionResult> RequestUsers(CancellationToken cancellationToken)
        {
            var correlationId = Guid.NewGuid();

            _logger.LogInformation(
                "Publishing UsersRequested. CorrelationId={CorrelationId}",
                correlationId);

            // Direct publish — Notification has no DbContext write to bundle
            // this with, and the bus-outbox isn't registered for this service.
            using (_deliveryScope.Use(DeliveryMode.Direct))
            {
                await _eventBus.Publish(new UsersRequested(
                        Sport.All,
                        null,
                        correlationId,
                        Guid.NewGuid()),
                    cancellationToken);
            }

            return Accepted(new { correlationId });
        }
    }
}
