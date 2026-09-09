#nullable enable

using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Seasons;
using SportsData.Notification.Application.Reminders;
using SportsData.Notification.Infrastructure.Data;
using SportsData.Notification.Infrastructure.Data.Entities;

namespace SportsData.Notification.Application.Consumers
{
    /// <summary>
    /// A new AP poll dropped. Broadcast "AP Top 25 is out" to every user who
    /// is a member of at least one league for the poll's sport — the weekly
    /// re-engagement moment competing apps already exploit. See
    /// <c>docs/features/poll-release-notifications.md</c>.
    ///
    /// <para>
    /// First broadcast-class consumer: one event, many recipients. The claim
    /// is per user — atomic <see cref="NotificationPollRelease"/> insert on
    /// the unique <c>(UserId, SeasonPollWeekId)</c> index — so a redelivery
    /// (or a partial-failure retry) re-notifies only users whose claim never
    /// landed. Per the established v1 stance: a missing notification beats a
    /// duplicate.
    /// </para>
    ///
    /// <para>
    /// Deliberately NOT gated on <c>SeasonWeekId</c>: the known
    /// SeasonPollWeek → SeasonWeek linkage defect (preseason poll linked to
    /// the wrong week, some polls linked null) must not suppress this. A poll
    /// release is newsworthy regardless of week mapping. Non-AP polls
    /// (coaches, CFP) stay silent in v1.
    /// </para>
    /// </summary>
    public class SeasonPollWeekCreatedConsumer : IConsumer<SeasonPollWeekCreated>
    {
        /// <summary>
        /// ESPN's slug for the AP poll (<c>SeasonPollDocumentProcessor</c>
        /// maps <c>Slug = dto.Type</c>; the AP Top 25 ref carries
        /// <c>type: "ap"</c>).
        /// </summary>
        internal const string ApPollSlug = "ap";

        private readonly ILogger<SeasonPollWeekCreatedConsumer> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IPushDeviceFanout _deviceFanout;

        public SeasonPollWeekCreatedConsumer(
            ILogger<SeasonPollWeekCreatedConsumer> logger,
            AppDataContext dataContext,
            IDateTimeProvider dateTimeProvider,
            IPushDeviceFanout deviceFanout)
        {
            _logger = logger;
            _dataContext = dataContext;
            _dateTimeProvider = dateTimeProvider;
            _deviceFanout = deviceFanout;
        }

        public async Task Consume(ConsumeContext<SeasonPollWeekCreated> context)
        {
            var msg = context.Message;
            using var _ = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = msg.CorrelationId,
                ["SeasonPollWeekId"] = msg.SeasonPollWeekId,
                ["PollSlug"] = msg.PollSlug ?? string.Empty
            });

            if (!string.Equals(msg.PollSlug, ApPollSlug, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation(
                    "SeasonPollWeekCreated for non-AP poll '{PollSlug}'; no notification in v1.",
                    msg.PollSlug);
                return;
            }

            _logger.LogInformation("AP poll release detected; starting broadcast fan-out.");

            // Audience: distinct members of any league for the poll's sport.
            // Device presence is resolved per user inside the fan-out, and
            // prefs are prefetched below — both projections are local.
            var audience = await _dataContext.PickemGroupMembers
                .AsNoTracking()
                .Join(
                    _dataContext.PickemGroups.Where(g => g.Sport == msg.Sport),
                    m => m.PickemGroupId,
                    g => g.Id,
                    (m, _) => m.UserId)
                .Distinct()
                .ToListAsync(context.CancellationToken);

            if (audience.Count == 0)
            {
                _logger.LogInformation("AP poll release affects 0 users; nothing to send.");
                return;
            }

            // One query instead of one per recipient: the opted-out set is
            // tiny (defaults are ON), so materializing it is cheap.
            var optedOut = (await _dataContext.UserNotificationPreferences
                    .AsNoTracking()
                    .Where(p => !p.PollReleasedEnabled)
                    .Select(p => p.UserId)
                    .ToListAsync(context.CancellationToken))
                .ToHashSet();

            const string title = "AP Top 25 is out";
            const string body = "The new college football rankings just dropped. See who moved.";

            // FCM data payload — kind/target convention per MatchupDeepLink /
            // the invite consumer; the mobile tap handler dispatches on kind.
            var data = new Dictionary<string, string>
            {
                ["kind"] = "PollReleased",
                ["target"] = "rankings",
                ["sport"] = msg.Sport.ToString()
            };

            var sent = 0;
            foreach (var userId in audience)
            {
                // Atomic per-user claim: idempotent across redelivery, and a
                // crash mid-broadcast resumes with only the unclaimed users.
                var claim = new NotificationPollRelease
                {
                    UserId = userId,
                    SeasonPollWeekId = msg.SeasonPollWeekId,
                    SeasonPollId = msg.SeasonPollId,
                    Sport = msg.Sport,
                    CorrelationId = msg.CorrelationId,
                    Channel = "Fcm",
                    Result = "Dispatching",
                    AttemptedUtc = _dateTimeProvider.UtcNow()
                };
                _dataContext.NotificationPollReleases.Add(claim);

                try
                {
                    await _dataContext.SaveChangesAsync(context.CancellationToken);
                }
                catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
                {
                    // Already claimed (prior delivery or racing pod). Skip this
                    // user, keep walking the audience.
                    _dataContext.Entry(claim).State = EntityState.Detached;
                    continue;
                }

                if (optedOut.Contains(userId))
                {
                    await FinalizeAsync(claim, "Suppressed_UserOptedOut", null, context.CancellationToken);
                    continue;
                }

                var outcome = await _deviceFanout.SendToUserDevicesAsync(userId, title, body, data);
                switch (outcome.Result)
                {
                    case PushFanoutResult.NoDevices:
                        await FinalizeAsync(claim, "Suppressed_NoDevice", null, context.CancellationToken);
                        break;
                    case PushFanoutResult.Sent:
                        claim.Title = title;
                        claim.Body = body;
                        await FinalizeAsync(claim, "Sent", null, context.CancellationToken);
                        sent++;
                        break;
                    default:
                        claim.Title = title;
                        claim.Body = body;
                        await FinalizeAsync(claim, "Failed_FcmError", outcome.FailureReason, context.CancellationToken);
                        break;
                }
            }

            _logger.LogInformation(
                "AP poll broadcast complete. Audience={Audience}, Sent={Sent}.",
                audience.Count, sent);
        }

        private async Task FinalizeAsync(
            NotificationPollRelease claim,
            string result,
            string? failureReason,
            CancellationToken cancellationToken)
        {
            claim.Result = result;
            claim.FailureReason = failureReason;
            claim.ModifiedUtc = _dateTimeProvider.UtcNow();
            await _dataContext.SaveChangesAsync(cancellationToken);
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        {
            // Npgsql surfaces unique-violation as SQLSTATE 23505.
            return ex.InnerException is Npgsql.PostgresException pg && pg.SqlState == "23505";
        }
    }
}
