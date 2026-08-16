using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Infrastructure.Clients.Contest;
using SportsData.Core.Infrastructure.Clients.Contest.Queries;
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
    /// Enrichment: after the picker gate (so only line moves that actually
    /// notify someone cost a call), the contest is fetched from Producer via
    /// <c>IContestClientFactory</c> to put the matchup in the title
    /// ("Line moved: KC @ LV") and to carry deep-link context. Stacked
    /// number-only alerts were indistinguishable from one another. Enrichment
    /// is strictly additive — any failure, including an unconfigured client
    /// slot, falls back to the original number-only copy and still sends.
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
        private readonly IContestClientFactory _contestClientFactory;

        public ContestOddsUpdatedConsumer(
            ILogger<ContestOddsUpdatedConsumer> logger,
            AppDataContext dataContext,
            IDateTimeProvider dateTimeProvider,
            IPushNotificationSender pushSender,
            IContestClientFactory contestClientFactory)
        {
            _logger = logger;
            _dataContext = dataContext;
            _dateTimeProvider = dateTimeProvider;
            _pushSender = pushSender;
            _contestClientFactory = contestClientFactory;
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

            // Pickers in leagues whose scoring depends on the moved dimension.
            // The join to PickemGroups applies the PickType filter (ATS↔spread,
            // OverUnder↔total, StraightUp never) and drops picks whose league
            // projection hasn't landed yet. One physical line move → one push
            // per user regardless of how many qualifying leagues they picked
            // it in.
            //
            // The deep-link target league is taken from THIS filtered set, so
            // it is guaranteed to be a league where the line actually matters.
            // Resolving it from a separate "user's picks on this contest" query
            // would be wrong: users routinely pick the same contest in leagues
            // of mixed types, and the earliest of those is often StraightUp —
            // a league where the move is irrelevant. Ordered by the league's
            // CreatedUtc so the choice is deterministic across redelivery.
            var qualifyingPicks = await (
                from p in _dataContext.UserPicks.AsNoTracking()
                join g in _dataContext.PickemGroups.AsNoTracking() on p.PickemGroupId equals g.Id
                where p.ContestId == msg.ContestId
                    && ((spreadMoved && g.PickType == LeaguePickType.AgainstTheSpread)
                        || (totalMoved && g.PickType == LeaguePickType.OverUnder))
                select new { p.UserId, LeagueId = g.Id, g.CreatedUtc })
                .ToListAsync(context.CancellationToken);

            var targets = qualifyingPicks
                .GroupBy(x => x.UserId)
                .Select(grp => new
                {
                    UserId = grp.Key,
                    LeagueId = grp.OrderBy(x => x.CreatedUtc).ThenBy(x => x.LeagueId).First().LeagueId
                })
                .ToList();

            if (targets.Count == 0)
            {
                _logger.LogInformation(
                    "No pickers in odds-sensitive leagues for ContestId {ContestId}; nothing to notify.",
                    msg.ContestId);

                await WarnOnUnprojectedLeaguesAsync(msg.ContestId, context.CancellationToken);
                return;
            }

            // Enrich AFTER the picker gate: only line moves that actually
            // notify someone are worth a call to Producer. A failure here
            // costs the matchup name and the deep link, never the push.
            var contest = await TryGetContestAsync(msg, context.CancellationToken);

            var title = ComposeTitle(contest);
            var body = ComposeBody(msg, contest, spreadMoved, totalMoved);

            _logger.LogInformation(
                "Dispatching line-move notification to {PickerCount} picker(s). SpreadMoved={SpreadMoved}, TotalMoved={TotalMoved}, Enriched={Enriched}",
                targets.Count, spreadMoved, totalMoved, contest is not null);

            foreach (var target in targets)
            {
                var data = BuildDeepLinkData(msg, contest, target.LeagueId);
                await DispatchToUserAsync(target.UserId, msg, title, body, data, context.CancellationToken);
            }
        }

        /// <summary>
        /// Fetches the contest from Producer for notification copy + deep-link
        /// context. Returns null on ANY failure — an unconfigured client slot,
        /// a transport error, or a non-success result — so the caller falls back
        /// to the number-only copy this consumer shipped originally. Enrichment
        /// is strictly additive; it must never cost a notification.
        /// </summary>
        private async Task<SeasonContestDto> TryGetContestAsync(
            ContestOddsUpdated msg,
            CancellationToken cancellationToken)
        {
            try
            {
                var client = _contestClientFactory.Resolve(msg.Sport);
                var result = await client.GetContestById(msg.ContestId, cancellationToken);

                if (result is Success<GetContestByIdResponse> { Value.Contest: not null } success)
                    return success.Value.Contest;

                _logger.LogWarning(
                    "Contest lookup returned no contest for {ContestId}; sending un-enriched line-move copy.",
                    msg.ContestId);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The consumer itself is being cancelled — stop now rather than
                // logging a "lookup failure" and walking into the dispatch loop
                // with a dead token. The `when` guard is load-bearing: an
                // HttpClient TIMEOUT also surfaces as TaskCanceledException but
                // on a different token, and that case must still degrade to the
                // un-enriched copy rather than abandoning the notification.
                throw;
            }
            catch (Exception ex)
            {
                // Includes the unconfigured-client case: the factory always
                // returns a client, so a missing base address surfaces here as
                // an invalid-request-URI exception rather than a null.
                _logger.LogWarning(ex,
                    "Contest lookup failed for {ContestId}; sending un-enriched line-move copy.",
                    msg.ContestId);
            }

            return null;
        }

        /// <summary>
        /// Diagnostic for the silent-gap class of bug: picks exist on this
        /// contest but no league projection backs them, so the inner join above
        /// drops them and nobody is notified. Fails closed by design, but it
        /// used to fail closed INVISIBLY — on 2026-08-16 thirteen of sixteen
        /// AgainstTheSpread leagues were missing from the projection and no
        /// line-move notification had ever reached them. Remedy is
        /// <c>POST admin/backfill/pickemgroups</c>.
        /// </summary>
        private async Task WarnOnUnprojectedLeaguesAsync(Guid contestId, CancellationToken cancellationToken)
        {
            var unprojected = await (
                from p in _dataContext.UserPicks.AsNoTracking()
                where p.ContestId == contestId
                    && !_dataContext.PickemGroups.AsNoTracking().Any(g => g.Id == p.PickemGroupId)
                select p.PickemGroupId)
                .Distinct()
                .CountAsync(cancellationToken);

            if (unprojected > 0)
            {
                _logger.LogWarning(
                    "Picks exist on ContestId {ContestId} for {UnprojectedLeagueCount} league(s) with no local " +
                    "PickemGroup projection; those pickers cannot be targeted. Run admin/backfill/pickemgroups.",
                    contestId, unprojected);
            }
        }

        /// <summary>
        /// Deep-link payload consumed by the mobile tap handlers. Mirrors the
        /// kind/id contract established by UserInvitedToPickemGroupConsumer.
        /// Sport travels as the backend enum name; the client maps it to route
        /// segments via its own resolveSportLeague, so URL conventions stay
        /// owned by the client.
        /// </summary>
        private static Dictionary<string, string> BuildDeepLinkData(
            ContestOddsUpdated msg,
            SeasonContestDto contest,
            Guid leagueId)
        {
            var data = new Dictionary<string, string>
            {
                ["kind"] = "OddsChanged",
                ["target"] = "matchup",
                ["contestId"] = msg.ContestId.ToString(),
                ["sport"] = msg.Sport.ToString(),
                ["leagueId"] = leagueId.ToString()
            };

            if (contest?.Week is { } week)
                data["week"] = week.ToString();

            return data;
        }

        private async Task DispatchToUserAsync(
            Guid userId,
            ContestOddsUpdated msg,
            string title,
            string body,
            IReadOnlyDictionary<string, string> data,
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
                var result = await _pushSender.SendAsync(device.FcmToken, title, body, data, cancellationToken);
                if (result is Success<string>)
                    successCount++;
                else
                    // Dead token → prune the device (isolated best-effort save).
                    await _dataContext.MarkDeadDeviceForRemovalAsync(result, device.Id, _logger, cancellationToken);
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

        /// <summary>
        /// Title carries the matchup because that's the boldest line in the
        /// tray — stacked "Line moved" alerts were indistinguishable without
        /// it. ShortName is already the abbreviated form Producer stores
        /// ("KC @ LV"), which is what keeps the title inside the notification
        /// width. Falls back to the original bare title when un-enriched.
        /// </summary>
        private static string ComposeTitle(SeasonContestDto contest)
        {
            return string.IsNullOrWhiteSpace(contest?.ShortName)
                ? "Line moved"
                : $"Line moved: {contest.ShortName}";
        }

        private static string ComposeBody(
            ContestOddsUpdated msg,
            SeasonContestDto contest,
            bool spreadMoved,
            bool totalMoved)
        {
            // Provider name is included when present to anchor the move ("per
            // DraftKings"). MLB never reaches here (all-null deltas are gated
            // out upstream), so this only formats football lines.
            var via = string.IsNullOrWhiteSpace(msg.ProviderName) ? "" : $" ({msg.ProviderName})";

            // The full name disambiguates the abbreviation in the title. When
            // un-enriched we keep the original "a game you picked" phrasing so
            // the copy still reads as a sentence.
            var subject = string.IsNullOrWhiteSpace(contest?.Name)
                ? "a game you picked"
                : contest.Name;

            if (spreadMoved && totalMoved)
                return $"{subject}: spread {FormatLine(msg.OldSpread)} → {FormatLine(msg.NewSpread)}, total {FormatLine(msg.OldOverUnder)} → {FormatLine(msg.NewOverUnder)}{via}.";

            if (spreadMoved)
                return $"{subject}: spread {FormatLine(msg.OldSpread)} → {FormatLine(msg.NewSpread)}{via}.";

            return $"{subject}: total {FormatLine(msg.OldOverUnder)} → {FormatLine(msg.NewOverUnder)}{via}.";
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
