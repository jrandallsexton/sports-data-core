using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.PickemGroups;
using SportsData.Notification.Infrastructure.Data;
using SportsData.Notification.Infrastructure.Data.Entities;
using SportsData.Notification.Infrastructure.Notifications;

namespace SportsData.Notification.Application.Consumers
{
    /// <summary>
    /// A user joined a league. Drives the "#5 welcome + commissioner-side
    /// notification" pair from the catalog in
    /// <c>docs/architecture/notification-service-events-and-state.md</c> §2:
    ///
    /// <list type="bullet">
    ///   <item>Welcome push to the joining user (gated on
    ///   <see cref="UserNotificationPreferences.MembershipEnabled"/>).</item>
    ///   <item>"New member joined" push to the commissioner (same gate, same
    ///   category — both are membership-category notifications).</item>
    /// </list>
    ///
    /// <para>
    /// Idempotency uses the same atomic-claim pattern as
    /// <see cref="UserPickScoredConsumer"/> — see its docstring for the
    /// rationale (TOCTOU race, crash-vs-duplicate trade-off).
    /// </para>
    ///
    /// <para>
    /// <b>Known fat-payload gap:</b> the current event carries
    /// <c>GroupId, UserId, Sport, SeasonYear</c> only. To send useful copy we
    /// also need <c>LeagueName</c>, <c>JoinedDisplayName</c>, and
    /// <c>CommissionerUserId</c>. See design doc §4 — the canonical name for
    /// this fat event is <c>PickemGroupMembershipCreated</c>; today's
    /// <c>PickemGroupMemberAdded</c> is the existing record kept for
    /// compatibility, but the fattening pass should align names too.
    /// </para>
    /// </summary>
    public class PickemGroupMemberAddedConsumer : IConsumer<PickemGroupMemberAdded>
    {
        // NotificationMembership.FailureReason is varchar(512); per-device detail
        // stays in structured logs.
        private const int FailureReasonMaxLength = 512;
        private const string FailureReasonTruncationSuffix = "…(truncated)";

        private readonly ILogger<PickemGroupMemberAddedConsumer> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IPushNotificationSender _pushSender;

        public PickemGroupMemberAddedConsumer(
            ILogger<PickemGroupMemberAddedConsumer> logger,
            AppDataContext dataContext,
            IDateTimeProvider dateTimeProvider,
            IPushNotificationSender pushSender)
        {
            _logger = logger;
            _dataContext = dataContext;
            _dateTimeProvider = dateTimeProvider;
            _pushSender = pushSender;
        }

        public async Task Consume(ConsumeContext<PickemGroupMemberAdded> context)
        {
            var msg = context.Message;
            using var _ = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = msg.CorrelationId,
                ["GroupId"] = msg.GroupId,
                ["UserId"] = msg.UserId,
                ["Sport"] = msg.Sport
            });

            _logger.LogInformation("PickemGroupMemberAdded received.");

            // Atomic claim keyed on (UserId, LeagueId) — one welcome per user per
            // league. See UserPickScoredConsumer for the claim-first rationale.
            var claim = new NotificationMembership
            {
                UserId = msg.UserId,
                LeagueId = msg.GroupId,
                CorrelationId = msg.CorrelationId,
                Channel = "Fcm",
                Result = "Dispatching",
                AttemptedUtc = _dateTimeProvider.UtcNow()
            };
            _dataContext.NotificationMemberships.Add(claim);

            try
            {
                await _dataContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                _logger.LogInformation(
                    "PickemGroupMemberAdded already claimed for UserId {UserId}, LeagueId {LeagueId} (CorrelationId {CorrelationId}); skipping.",
                    msg.UserId, msg.GroupId, msg.CorrelationId);
                _dataContext.Entry(claim).State = EntityState.Detached;
                return;
            }

            // 1. Resolve the joining user's preferences + device(s).
            var prefs = await _dataContext.UserNotificationPreferences
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == msg.UserId);

            if (prefs is { MembershipEnabled: false })
            {
                _logger.LogInformation(
                    "Membership-category notifications disabled for UserId {UserId}; suppressing welcome push.",
                    msg.UserId);
                await FinalizeAsync(claim, "Suppressed_UserOptedOut");
                return;
            }

            var devices = await _dataContext.UserDevices
                .AsNoTracking()
                .Where(d => d.UserId == msg.UserId && d.NotificationsEnabled)
                .ToListAsync();

            if (devices.Count == 0)
            {
                _logger.LogInformation(
                    "No active UserDevice rows for UserId {UserId}; suppressing welcome push.",
                    msg.UserId);
                await FinalizeAsync(claim, "Suppressed_NoDevice");
                return;
            }

            // 2. Send welcome push. League name comes from the local
            // PickemGroup projection (seeded by the backfill chain). When the
            // projection is missing — Notification booted before backfill ran,
            // or the league is brand-new and the projection hasn't caught up —
            // fall back to the unscoped copy.
            var leagueName = await _dataContext.PickemGroups
                .AsNoTracking()
                .Where(g => g.Id == msg.GroupId)
                .Select(g => g.Name)
                .FirstOrDefaultAsync();

            const string title = "Welcome";
            var body = leagueName is not null
                ? $"You've joined {leagueName}!"
                : "You've joined a new league.";

            // 3. Commissioner-side notification is deferred. We now have
            // CommissionerUserId locally via the PickemGroup projection — the
            // remaining missing piece is the joining user's DisplayName lookup
            // and a second dispatch loop against the commissioner's prefs +
            // devices. Layered in as a follow-up.

            var successCount = 0;
            var failureReasons = new List<string>();
            foreach (var device in devices)
            {
                var sendResult = await _pushSender.SendAsync(device.FcmToken, title, body);
                if (sendResult is Success<string>)
                {
                    successCount++;
                }
                else if (sendResult is Failure<string> failure)
                {
                    var reason = failure.Errors.FirstOrDefault()?.ErrorMessage ?? "unknown";
                    failureReasons.Add($"{device.Platform}:{reason}");
                    // Dead token → prune the device (isolated best-effort save).
                    await _dataContext.MarkDeadDeviceForRemovalAsync(sendResult, device.Id, _logger);
                }
            }

            claim.Title = title;
            claim.Body = body;
            claim.Result = successCount > 0 ? "Sent" : "Failed_FcmError";
            claim.FailureReason = ComposeFailureReason(failureReasons);
            claim.ModifiedUtc = _dateTimeProvider.UtcNow();

            await _dataContext.SaveChangesAsync();
        }

        private async Task FinalizeAsync(NotificationMembership claim, string result)
        {
            claim.Result = result;
            claim.ModifiedUtc = _dateTimeProvider.UtcNow();
            await _dataContext.SaveChangesAsync();
        }

        private static string ComposeFailureReason(List<string> failureReasons)
        {
            if (failureReasons.Count == 0)
                return null;

            var joined = string.Join("; ", failureReasons);
            if (joined.Length <= FailureReasonMaxLength)
                return joined;

            var cutoff = FailureReasonMaxLength - FailureReasonTruncationSuffix.Length;
            return joined.Substring(0, cutoff) + FailureReasonTruncationSuffix;
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        {
            // Npgsql surfaces unique-violation as SQLSTATE 23505.
            return ex.InnerException is Npgsql.PostgresException pg && pg.SqlState == "23505";
        }
    }
}
