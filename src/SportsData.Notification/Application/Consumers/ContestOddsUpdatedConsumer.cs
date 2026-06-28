using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Notification.Infrastructure.Data;
using SportsData.Notification.Infrastructure.Data.Entities;
using SportsData.Notification.Infrastructure.Notifications;

namespace SportsData.Notification.Application.Consumers
{
    /// <summary>
    /// The betting line on a contest moved. Notify every user who already has a
    /// pick on that contest — "you committed at one number, the market has since
    /// moved" — so they can revisit before the contest locks.
    ///
    /// <para>
    /// Targeting joins the local <see cref="UserPick"/> projection (fed by
    /// <c>UserPickMade</c>) to the <see cref="PickemGroup"/> projection and
    /// filters on the league's <c>PickType</c>: a line move only matters where
    /// scoring depends on the odds. Spread movement targets
    /// <see cref="LeaguePickType.AgainstTheSpread"/> leagues; total movement
    /// targets <see cref="LeaguePickType.OverUnder"/> leagues;
    /// <see cref="LeaguePickType.StraightUp"/> leagues never qualify (they don't
    /// care about the line). Pickers only — NOT all league members. A user who
    /// picked the same contest in several qualifying leagues is notified
    /// <b>once</b> (the distinct on UserId dedups across leagues). The inner
    /// join also means a pick whose league projection hasn't landed yet is
    /// simply not notified rather than mis-targeted.
    /// </para>
    ///
    /// <para>
    /// Movement gate: only spread or total movement is actionable. The football
    /// path carries Old/New spread &amp; total on the event; the MLB path
    /// replaces a set of per-provider rows and so publishes all-null deltas —
    /// with no single old/new pair there's nothing to report, and the equality
    /// check below naturally treats "all null" as "no movement" and skips.
    /// </para>
    ///
    /// <para>
    /// Per-user dispatch mirrors <see cref="UserPickScoredConsumer"/>: atomic
    /// NotificationLog claim on the unique <c>(CorrelationId, UserId, Channel)</c>
    /// index (idempotent across redelivery and across pickers of the same
    /// contest) → prefs → devices → send → terminal update. A claim race for one
    /// user detaches and continues to the next; one user's failure never blocks
    /// the rest of the fan-out.
    /// </para>
    /// </summary>
    public class ContestOddsUpdatedConsumer : IConsumer<ContestOddsUpdated>
    {
        private readonly ILogger<ContestOddsUpdatedConsumer> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IPushNotificationSender _pushSender;

        public ContestOddsUpdatedConsumer(
            ILogger<ContestOddsUpdatedConsumer> logger,
            AppDataContext dataContext,
            IDateTimeProvider dateTimeProvider,
            IPushNotificationSender pushSender)
        {
            _logger = logger;
            _dataContext = dataContext;
            _dateTimeProvider = dateTimeProvider;
            _pushSender = pushSender;
        }

        public async Task Consume(ConsumeContext<ContestOddsUpdated> context)
        {
            var msg = context.Message;
            using var _ = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = msg.CorrelationId,
                ["ContestId"] = msg.ContestId,
                ["Sport"] = msg.Sport
            });

            _logger.LogInformation("ContestOddsUpdated received.");

            var spreadMoved = msg.OldSpread != msg.NewSpread;
            var totalMoved = msg.OldOverUnder != msg.NewOverUnder;

            if (!spreadMoved && !totalMoved)
            {
                _logger.LogInformation("No spread or total movement on the event; skipping.");
                return;
            }

            // Pickers in leagues whose scoring depends on the moved dimension,
            // deduped across leagues. The join to PickemGroups applies the
            // PickType filter (ATS↔spread, OverUnder↔total, StraightUp never)
            // and drops picks whose league projection hasn't landed yet. One
            // physical line move → one push per user regardless of how many
            // qualifying leagues they picked it in.
            var userIds = await (
                from p in _dataContext.UserPicks.AsNoTracking()
                join g in _dataContext.PickemGroups.AsNoTracking() on p.PickemGroupId equals g.Id
                where p.ContestId == msg.ContestId
                    && ((spreadMoved && g.PickType == LeaguePickType.AgainstTheSpread)
                        || (totalMoved && g.PickType == LeaguePickType.OverUnder))
                select p.UserId)
                .Distinct()
                .ToListAsync(context.CancellationToken);

            if (userIds.Count == 0)
            {
                _logger.LogInformation(
                    "No pickers in odds-sensitive leagues for ContestId {ContestId}; nothing to notify.",
                    msg.ContestId);
                return;
            }

            var title = "Line moved";
            var body = ComposeBody(msg, spreadMoved, totalMoved);

            _logger.LogInformation(
                "Dispatching line-move notification to {PickerCount} picker(s). SpreadMoved={SpreadMoved}, TotalMoved={TotalMoved}",
                userIds.Count, spreadMoved, totalMoved);

            foreach (var userId in userIds)
            {
                await DispatchToUserAsync(userId, msg, title, body, context.CancellationToken);
            }
        }

        private async Task DispatchToUserAsync(
            Guid userId,
            ContestOddsUpdated msg,
            string title,
            string body,
            CancellationToken cancellationToken)
        {
            using var _ = _logger.BeginScope(new Dictionary<string, object> { ["UserId"] = userId });

            // Atomic claim per user, keyed on (CorrelationId, UserId, Channel).
            // Same correlation across all pickers of this contest, so each user
            // is claimed once even on redelivery; the loser of any race detaches
            // and we move on without dispatching twice.
            var claim = new NotificationLog
            {
                UserId = userId,
                CorrelationId = msg.CorrelationId,
                Category = "OddsChanged",
                Channel = "Fcm",
                Result = "Dispatching",
                AttemptedUtc = _dateTimeProvider.UtcNow()
            };
            _dataContext.NotificationLog.Add(claim);

            try
            {
                await _dataContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                // A prior attempt already claimed this (CorrelationId, UserId,
                // Channel). We skip unconditionally — including when that row is
                // still "Dispatching" from a crashed attempt. This is the same
                // deliberate v1 tradeoff as UserPickScoredConsumer: a missing
                // notification beats a duplicate one (a crash can land after the
                // FCM send but before the terminal update, so resuming a stale
                // claim risks re-sending). Stale Dispatching rows are left for a
                // future cleanup job shared across consumers, not recovered here.
                _logger.LogInformation(
                    "Line-move notification already claimed for CorrelationId {CorrelationId}, UserId {UserId}; skipping.",
                    msg.CorrelationId, userId);
                _dataContext.Entry(claim).State = EntityState.Detached;
                return;
            }

            var prefs = await _dataContext.UserNotificationPreferences
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

            if (prefs is { OddsChangedEnabled: false })
            {
                await FinalizeAsync(claim, "Suppressed_UserOptedOut", cancellationToken);
                return;
            }

            var devices = await _dataContext.UserDevices
                .AsNoTracking()
                .Where(d => d.UserId == userId && d.NotificationsEnabled)
                .ToListAsync(cancellationToken);

            if (devices.Count == 0)
            {
                await FinalizeAsync(claim, "Suppressed_NoDevice", cancellationToken);
                return;
            }

            var successCount = 0;
            foreach (var device in devices)
            {
                var result = await _pushSender.SendAsync(device.FcmToken, title, body);
                if (result is Success<string>)
                    successCount++;
            }

            claim.Title = title;
            claim.Body = body;
            claim.Result = successCount > 0 ? "Sent" : "Failed_FcmError";
            claim.ModifiedUtc = _dateTimeProvider.UtcNow();

            await _dataContext.SaveChangesAsync(cancellationToken);
        }

        private async Task FinalizeAsync(NotificationLog claim, string result, CancellationToken cancellationToken)
        {
            claim.Result = result;
            claim.ModifiedUtc = _dateTimeProvider.UtcNow();
            await _dataContext.SaveChangesAsync(cancellationToken);
        }

        private static string ComposeBody(ContestOddsUpdated msg, bool spreadMoved, bool totalMoved)
        {
            // The event carries numbers but no team names, so the copy is
            // number-only. Provider name is included when present to anchor the
            // move ("per DraftKings"). MLB never reaches here (all-null deltas
            // are gated out upstream), so this only formats football lines.
            var via = string.IsNullOrWhiteSpace(msg.ProviderName) ? "" : $" ({msg.ProviderName})";

            if (spreadMoved && totalMoved)
                return $"The line moved on a game you picked: spread {FormatLine(msg.OldSpread)} → {FormatLine(msg.NewSpread)}, total {FormatLine(msg.OldOverUnder)} → {FormatLine(msg.NewOverUnder)}{via}.";

            if (spreadMoved)
                return $"The spread moved on a game you picked: {FormatLine(msg.OldSpread)} → {FormatLine(msg.NewSpread)}{via}.";

            return $"The total moved on a game you picked: {FormatLine(msg.OldOverUnder)} → {FormatLine(msg.NewOverUnder)}{via}.";
        }

        private static string FormatLine(decimal? value)
        {
            // No value on one side means the number appeared/disappeared rather
            // than shifting; show an em dash so the copy still reads.
            return value?.ToString("0.#") ?? "—";
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        {
            // Npgsql surfaces unique-violation as SQLSTATE 23505.
            return ex.InnerException is Npgsql.PostgresException pg && pg.SqlState == "23505";
        }
    }
}
