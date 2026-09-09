#nullable enable

using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.PickemGroups;
using SportsData.Notification.Application.Reminders;
using SportsData.Notification.Infrastructure.Data;
using SportsData.Notification.Infrastructure.Data.Entities;

namespace SportsData.Notification.Application.Consumers
{
    /// <summary>
    /// A league's week slate was generated — tell its members the picks are
    /// open. The opening bookend to the pick-deadline reminder's closing
    /// bookend. See <c>docs/features/poll-release-notifications.md</c>.
    ///
    /// <para>
    /// The publisher (API's <c>MatchupScheduleProcessor</c>) only emits when
    /// NEW matchups were inserted and the week isn't completed, but a
    /// ranked-league refresh pass legitimately re-publishes when late
    /// contests land mid-week. The per-user claim on the unique
    /// <c>(UserId, LeagueId, SeasonYear, SeasonWeek)</c> index makes the
    /// first event win: one "week is ready" push per user per league-week,
    /// later additions stay silent in v1.
    /// </para>
    /// </summary>
    public class PickemGroupWeekMatchupsGeneratedConsumer : IConsumer<PickemGroupWeekMatchupsGenerated>
    {
        private readonly ILogger<PickemGroupWeekMatchupsGeneratedConsumer> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IPushDeviceFanout _deviceFanout;

        public PickemGroupWeekMatchupsGeneratedConsumer(
            ILogger<PickemGroupWeekMatchupsGeneratedConsumer> logger,
            AppDataContext dataContext,
            IDateTimeProvider dateTimeProvider,
            IPushDeviceFanout deviceFanout)
        {
            _logger = logger;
            _dataContext = dataContext;
            _dateTimeProvider = dateTimeProvider;
            _deviceFanout = deviceFanout;
        }

        public async Task Consume(ConsumeContext<PickemGroupWeekMatchupsGenerated> context)
        {
            var msg = context.Message;
            using var _ = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = msg.CorrelationId,
                ["GroupId"] = msg.GroupId,
                ["SeasonYear"] = msg.SeasonYear ?? 0,
                ["WeekNumber"] = msg.WeekNumber
            });

            // The publisher (MatchupScheduleProcessor) always sets SeasonYear
            // from its command; a null here is a malformed event, and claiming
            // under a fabricated year-0 dedupe key would both lie in the audit
            // table and block the real year's notification if a corrected
            // event ever arrived.
            if (msg.SeasonYear is null)
            {
                _logger.LogWarning(
                    "PickemGroupWeekMatchupsGenerated for league {GroupId} carried no SeasonYear; skipping fan-out.",
                    msg.GroupId);
                return;
            }

            // League name for the copy. The projection is seeded at league
            // creation and converged by backfill; a missing row means the
            // member projection is missing too, so there is nobody to notify
            // yet — log and let the projection backfill catch up. (The event
            // will not refire for this week, an accepted v1 gap.)
            var groupName = await _dataContext.PickemGroups
                .AsNoTracking()
                .Where(g => g.Id == msg.GroupId)
                .Select(g => (string?)g.Name)
                .FirstOrDefaultAsync(context.CancellationToken);

            if (groupName is null)
            {
                _logger.LogWarning(
                    "PickemGroupWeekMatchupsGenerated for unknown league {GroupId}; projection not seeded yet. Skipping.",
                    msg.GroupId);
                return;
            }

            var members = await _dataContext.PickemGroupMembers
                .AsNoTracking()
                .Where(m => m.PickemGroupId == msg.GroupId)
                .Select(m => m.UserId)
                .ToListAsync(context.CancellationToken);

            if (members.Count == 0)
            {
                _logger.LogInformation("League {GroupId} has no member projections; nothing to send.", msg.GroupId);
                return;
            }

            var optedOut = (await _dataContext.UserNotificationPreferences
                    .AsNoTracking()
                    .Where(p => !p.MatchupsReadyEnabled)
                    .Select(p => p.UserId)
                    .ToListAsync(context.CancellationToken))
                .ToHashSet();

            var title = $"Week {msg.WeekNumber} matchups are ready";
            var body = $"Week {msg.WeekNumber} matchups are set in {groupName} — make your picks.";

            // FCM data payload — kind/target convention per MatchupDeepLink /
            // the invite consumer; lands on the league's picks screen.
            var data = new Dictionary<string, string>
            {
                ["kind"] = "MatchupsReady",
                ["target"] = "picks",
                ["leagueId"] = msg.GroupId.ToString(),
                ["week"] = msg.WeekNumber.ToString(),
                ["sport"] = msg.Sport.ToString()
            };

            var sent = 0;
            foreach (var userId in members)
            {
                var claim = new NotificationMatchupsReady
                {
                    UserId = userId,
                    LeagueId = msg.GroupId,
                    SeasonYear = msg.SeasonYear.Value,
                    SeasonWeek = msg.WeekNumber,
                    CorrelationId = msg.CorrelationId,
                    Channel = "Fcm",
                    Result = "Dispatching",
                    AttemptedUtc = _dateTimeProvider.UtcNow()
                };
                _dataContext.NotificationMatchupsReady.Add(claim);

                try
                {
                    await _dataContext.SaveChangesAsync(context.CancellationToken);
                }
                catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
                {
                    _dataContext.Entry(claim).State = EntityState.Detached;

                    // Finalized row = already notified for this league-week
                    // (redelivery, or a ranked-league refresh re-publish) —
                    // first event won, skip. A row stranded at "Dispatching"
                    // is a crash orphan: reclaim and dispatch, same broadcast
                    // trade-off as SeasonPollWeekCreatedConsumer (a stranded
                    // user misses the week's slate; the duplicate window is
                    // the milliseconds between FCM send and finalize).
                    var existing = await _dataContext.NotificationMatchupsReady
                        .FirstOrDefaultAsync(
                            r => r.UserId == userId
                                 && r.LeagueId == msg.GroupId
                                 && r.SeasonYear == msg.SeasonYear.Value
                                 && r.SeasonWeek == msg.WeekNumber,
                            context.CancellationToken);

                    if (existing is null || existing.Result != "Dispatching")
                    {
                        continue;
                    }

                    existing.AttemptedUtc = _dateTimeProvider.UtcNow();
                    existing.CorrelationId = msg.CorrelationId;
                    existing.ModifiedUtc = _dateTimeProvider.UtcNow();
                    await _dataContext.SaveChangesAsync(context.CancellationToken);
                    claim = existing;
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
                "Matchups-ready fan-out complete for league {GroupId} week {WeekNumber}. Members={Members}, Sent={Sent}.",
                msg.GroupId, msg.WeekNumber, members.Count, sent);
        }

        private async Task FinalizeAsync(
            NotificationMatchupsReady claim,
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
