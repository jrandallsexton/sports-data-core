using System.Globalization;

using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Picks;
using SportsData.Notification.Infrastructure.Data;
using SportsData.Notification.Infrastructure.Data.Entities;
using SportsData.Notification.Infrastructure.Notifications;

namespace SportsData.Notification.Application.Consumers
{
    /// <summary>
    /// Headline v1 consumer: a user's pick was scored, send the result push.
    /// Every member who picked a finalized contest gets one of these events.
    ///
    /// <para>
    /// Orchestration: <b>atomic-claim</b> via NotificationUserPick insert
    /// (relies on the unique <c>(UserId, PickId)</c> index)
    /// → resolve prefs → resolve devices → dispatch via
    /// <see cref="IPushNotificationSender"/> → update the row with terminal outcome.
    /// </para>
    ///
    /// <para>
    /// The claim-first pattern closes a TOCTOU race the prior AnyAsync-then-insert
    /// flow had: two concurrent redeliveries could both read "no row" and both
    /// dispatch before either inserted. Now the first INSERT wins the unique
    /// constraint; the loser gets <see cref="DbUpdateException"/> and returns
    /// without dispatching.
    /// </para>
    ///
    /// <para>
    /// Failure mode: if the consumer crashes between claim and terminal update,
    /// the row sits at <c>Result="Dispatching"</c> indefinitely and any
    /// redelivery is suppressed by the unique constraint. We've chosen a
    /// missing notification over a duplicate notification for v1 — duplicates
    /// are worse UX than absences. A future cleanup job could rerun stale
    /// Dispatching rows if needed.
    /// </para>
    /// </summary>
    public class UserPickScoredConsumer : IConsumer<UserPickScored>
    {
        // NotificationUserPick.FailureReason is varchar(512). Truncate the joined
        // per-device summary so we don't trip Postgres's length check on a
        // user with many failing devices; per-device detail stays in logs.
        private const int FailureReasonMaxLength = 512;
        private const string FailureReasonTruncationSuffix = "…(truncated)";

        private readonly ILogger<UserPickScoredConsumer> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IPushNotificationSender _pushSender;

        public UserPickScoredConsumer(
            ILogger<UserPickScoredConsumer> logger,
            AppDataContext dataContext,
            IDateTimeProvider dateTimeProvider,
            IPushNotificationSender pushSender)
        {
            _logger = logger;
            _dataContext = dataContext;
            _dateTimeProvider = dateTimeProvider;
            _pushSender = pushSender;
        }

        public async Task Consume(ConsumeContext<UserPickScored> context)
        {
            var msg = context.Message;
            using var _ = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = msg.CorrelationId,
                ["UserId"] = msg.UserId,
                ["ContestId"] = msg.ContestId,
                ["LeagueId"] = msg.LeagueId,
                ["Sport"] = msg.Sport
            });

            _logger.LogInformation("UserPickScored received.");

            // Atomic claim. Insert a NotificationUserPick row in Dispatching
            // state BEFORE doing any work. The (UserId, PickId) unique index is
            // the idempotency key — one push per pick, ever. If another consumer
            // (redelivery, or a re-score from the cron backstop on a different
            // correlation chain) beat us to it, the unique constraint throws
            // DbUpdateException and we bail without dispatching anything.
            // CorrelationId is carried for tracing only, not part of the key.
            var claim = new NotificationUserPick
            {
                UserId = msg.UserId,
                PickId = msg.PickId,
                ContestId = msg.ContestId,
                LeagueId = msg.LeagueId,
                CorrelationId = msg.CorrelationId,
                Channel = "Fcm",
                Result = "Dispatching",
                AttemptedUtc = _dateTimeProvider.UtcNow()
            };
            _dataContext.NotificationUserPicks.Add(claim);

            try
            {
                await _dataContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                _logger.LogInformation(
                    "UserPickScored already claimed for PickId {PickId}, UserId {UserId} (CorrelationId {CorrelationId}); skipping.",
                    msg.PickId, msg.UserId, msg.CorrelationId);
                _dataContext.Entry(claim).State = EntityState.Detached;
                return;
            }

            // From here on we own the dispatch. Any terminal state writes
            // UPDATE the claim row rather than insert a new one.
            var prefs = await _dataContext.UserNotificationPreferences
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == msg.UserId);

            if (prefs is { PickResultEnabled: false })
            {
                await FinalizeAsync(claim, "Suppressed_UserOptedOut");
                return;
            }

            var devices = await _dataContext.UserDevices
                .AsNoTracking()
                .Where(d => d.UserId == msg.UserId && d.NotificationsEnabled)
                .ToListAsync();

            if (devices.Count == 0)
            {
                await FinalizeAsync(claim, "Suppressed_NoDevice");
                return;
            }

            // Body personalization via the local PickemGroup projection (seeded
            // by the PickemGroupsRequested → PickemGroupDataPublished backfill
            // chain). When the projection is missing — Notification booted
            // before the backfill ran, or the league is brand-new and the
            // projection hasn't caught up — fall back to the unscoped copy.
            // Team names still ride on the event (nullable on the contract)
            // and are absent when the publisher hasn't fattened them yet.
            var leagueName = await _dataContext.PickemGroups
                .AsNoTracking()
                .Where(g => g.Id == msg.LeagueId)
                .Select(g => g.Name)
                .FirstOrDefaultAsync();

            var title = msg.IsCorrect == true ? "Nice pick!" : "Tough loss";
            var body = ComposeBody(msg, leagueName);

            // Per-device dispatch. Single-token send rather than multicast
            // because v1's IPushNotificationSender surface is one-at-a-time;
            // multicast batching can come later if device counts per user
            // grow past a handful. Aggregate outcome:
            //   - any success → "Sent"
            //   - all failures → "Failed_FcmError" with reasons captured
            // Mixed outcomes still log "Sent" because the user did get the
            // push; per-device failure reasons stay in the structured logs.
            var successCount = 0;
            var failureReasons = new List<string>();
            foreach (var device in devices)
            {
                var result = await _pushSender.SendAsync(device.FcmToken, title, body);
                if (result is Success<string>)
                {
                    successCount++;
                }
                else if (result is Failure<string> failure)
                {
                    var reason = failure.Errors.FirstOrDefault()?.ErrorMessage ?? "unknown";
                    failureReasons.Add($"{device.Platform}:{reason}");
                    // Dead token → prune the device; flushed by the SaveChanges below.
                    _dataContext.MarkDeadDeviceForRemoval(result, device.Id, _logger);
                }
            }

            claim.Title = title;
            claim.Body = body;
            claim.Result = successCount > 0 ? "Sent" : "Failed_FcmError";
            claim.FailureReason = ComposeFailureReason(failureReasons);
            claim.ModifiedUtc = _dateTimeProvider.UtcNow();

            await _dataContext.SaveChangesAsync();
        }

        private async Task FinalizeAsync(NotificationUserPick claim, string result)
        {
            claim.Result = result;
            claim.ModifiedUtc = _dateTimeProvider.UtcNow();
            await _dataContext.SaveChangesAsync();
        }

        // "BOS" for a straight-up pick; "BOS +2.5" / "BOS -3" for ATS. The spread
        // is a signed number from the picked team's perspective (negative =
        // favored). Invariant formatting so a comma-decimal server locale can't
        // render "2,5".
        private static string FormatPickLabel(string abbreviation, double? pickedSpread)
        {
            if (pickedSpread is not { } spread)
                return abbreviation;

            var sign = spread > 0 ? "+" : string.Empty;
            return $"{abbreviation} {sign}{spread.ToString("0.#", CultureInfo.InvariantCulture)}";
        }

        private static string ComposeBody(UserPickScored msg, string leagueName)
        {
            // Preferred shape (scoreline + mark), picked team first. This service
            // owns the copy: the event carries structured facts (abbreviations,
            // PickedIsHome, PickedSpread) and we format here. The "you picked"
            // clause appends the picked side's spread for ATS so a covered loss /
            // uncovered win reads correctly:
            //   "{League}: BOS 3, NYY 2 — you picked BOS ✓"          (SU win)
            //   "{League}: BOS 2, NYY 3 — you picked BOS +2.5 ✓"     (ATS, covered loss)
            // Requires the fattened fields; falls through to the generic shapes
            // below for Over/Under picks or older/unfattened events.
            if (msg.AwayAbbreviation is not null
                && msg.HomeAbbreviation is not null
                && msg.PickedIsHome is not null)
            {
                var pickedIsHome = msg.PickedIsHome.Value;
                var pickedAbbr = pickedIsHome ? msg.HomeAbbreviation : msg.AwayAbbreviation;
                var oppAbbr = pickedIsHome ? msg.AwayAbbreviation : msg.HomeAbbreviation;
                var pickedScore = pickedIsHome ? msg.HomeScore : msg.AwayScore;
                var oppScore = pickedIsHome ? msg.AwayScore : msg.HomeScore;
                var mark = msg.IsCorrect == true ? "✓" : "✗";
                var pickLabel = FormatPickLabel(pickedAbbr, msg.PickedSpread);

                // League always present: prefer the local projection, fall back
                // to the name carried on the event.
                var league = leagueName ?? msg.LeagueName;
                return $"{league}: {pickedAbbr} {pickedScore}, {oppAbbr} {oppScore} — you picked {pickLabel} {mark}";
            }

            // Fallback shapes by what we have. League name comes from the local
            // projection; team names are nullable on the event payload until
            // publisher-side fattening lands (FranchiseSeason joins).
            var outcome = msg.IsCorrect == true ? "Your pick won." : "Your pick lost.";
            var scoreLine = $"Final {msg.AwayScore}–{msg.HomeScore}.";

            var haveTeams = msg.AwayName is not null && msg.HomeName is not null;
            var teamLine = haveTeams
                ? $"{msg.AwayName} {msg.AwayScore} @ {msg.HomeName} {msg.HomeScore}"
                : null;

            if (leagueName is not null && haveTeams)
                return $"{leagueName}: {outcome} {teamLine}.";
            if (leagueName is not null)
                return $"{leagueName}: {outcome} {scoreLine}";
            if (haveTeams)
                return $"Final: {teamLine}";

            return $"{outcome} {scoreLine}";
        }

        private static string ComposeFailureReason(List<string> failureReasons)
        {
            if (failureReasons.Count == 0)
                return null;

            var joined = string.Join("; ", failureReasons);
            if (joined.Length <= FailureReasonMaxLength)
                return joined;

            // Reserve room for the suffix; full per-device detail stays in
            // structured logs via FirebasePushNotificationSender.
            var cutoff = FailureReasonMaxLength - FailureReasonTruncationSuffix.Length;
            return joined.Substring(0, cutoff) + FailureReasonTruncationSuffix;
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        {
            // Npgsql surfaces unique-violation as SQLSTATE 23505. Inspecting
            // SqlState on the inner PostgresException is the load-bearing
            // check; the EF-side DbUpdateException is too generic on its own.
            return ex.InnerException is Npgsql.PostgresException pg && pg.SqlState == "23505";
        }
    }
}
