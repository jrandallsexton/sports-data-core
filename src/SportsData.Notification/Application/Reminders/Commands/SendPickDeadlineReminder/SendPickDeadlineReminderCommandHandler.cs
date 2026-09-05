#nullable enable

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using SportsData.Core.Common;
using SportsData.Notification.Config;
using SportsData.Notification.Infrastructure.Data;
using SportsData.Notification.Infrastructure.Data.Entities;

namespace SportsData.Notification.Application.Reminders.Commands.SendPickDeadlineReminder;

public interface ISendPickDeadlineReminderCommandHandler
{
    /// <summary>
    /// Hangfire-invoked v2 wave-model pick-deadline dispatch:
    /// <paramref name="waveAnchorUtc"/> is the earliest kickoff of the wave
    /// this fire covers. The missing-pick gate and copy are computed here
    /// at fire time.
    /// </summary>
    Task ExecuteAsync(Guid userId, Guid pickemGroupId, int seasonWeek, DateTime fireTimeUtc, DateTime waveAnchorUtc);
}

/// <summary>
/// Atomic-claim + dispatch for the pick-deadline reminder (same pattern as
/// the event-driven consumers, e.g. <c>UserPickScoredConsumer</c>).
///
/// <para>
/// Idempotency rides on the typed claim table's natural key —
/// <c>NotificationPickDeadline (UserId, LeagueId, SeasonWeek,
/// FireTimeUtc)</c>. The <c>FireTimeUtc</c> component is the version
/// anchor: a Hangfire retry of the same fire collides and is suppressed,
/// while a reschedule (new fire-time) is a new row and re-fires. The
/// deterministic CorrelationId is a trace id only.
/// </para>
/// </summary>
public class SendPickDeadlineReminderCommandHandler : ISendPickDeadlineReminderCommandHandler
{
    private readonly ILogger<SendPickDeadlineReminderCommandHandler> _logger;
    private readonly AppDataContext _dataContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IStaleFireGuard _staleFireGuard;
    private readonly IPushDeviceFanout _fanout;
    private readonly NotificationConfig _config;

    public SendPickDeadlineReminderCommandHandler(
        ILogger<SendPickDeadlineReminderCommandHandler> logger,
        AppDataContext dataContext,
        IDateTimeProvider dateTimeProvider,
        IStaleFireGuard staleFireGuard,
        IPushDeviceFanout fanout,
        IOptions<NotificationConfig> config)
    {
        _logger = logger;
        _dataContext = dataContext;
        _dateTimeProvider = dateTimeProvider;
        _staleFireGuard = staleFireGuard;
        _fanout = fanout;
        _config = config.Value;
    }

    public async Task ExecuteAsync(Guid userId, Guid pickemGroupId, int seasonWeek, DateTime fireTimeUtc, DateTime waveAnchorUtc)
    {
        // fireTimeUtc IS the version anchor — what the scheduler intended
        // this job to fire at. Used both for the deterministic trace id and
        // for the stale-fire check below (an orphan reads the
        // PendingScheduledJob row, sees the scheduler has since moved to a
        // different ScheduledFireUtc, and aborts before sending the
        // wrong-time push).
        var correlationId = ReminderCorrelation.DeterministicCorrelationId(
            "PickDeadline", userId, pickemGroupId, seasonWeek, fireTimeUtc.Ticks);

        using var _ = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["UserId"] = userId,
            ["PickemGroupId"] = pickemGroupId,
            ["SeasonWeek"] = seasonWeek,
            ["FireTimeUtc"] = fireTimeUtc,
            ["WaveAnchorUtc"] = waveAnchorUtc
        });

        _logger.LogInformation("SendPickDeadlineReminder invoked.");

        // Atomic claim on (UserId, LeagueId, SeasonWeek, FireTimeUtc): a
        // Hangfire retry of the same fire collides (suppressed) while a
        // reschedule (new FireTimeUtc) re-fires. See UserPickScoredConsumer
        // for the claim-first rationale.
        var claim = new NotificationPickDeadline
        {
            UserId = userId,
            LeagueId = pickemGroupId,
            SeasonWeek = seasonWeek,
            FireTimeUtc = fireTimeUtc,
            WaveAnchorUtc = waveAnchorUtc,
            CorrelationId = correlationId,
            Channel = "Fcm",
            Result = "Dispatching",
            AttemptedUtc = _dateTimeProvider.UtcNow()
        };
        _dataContext.NotificationPickDeadlines.Add(claim);

        try
        {
            await _dataContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            _logger.LogInformation(
                "PickDeadline reminder already dispatched for CorrelationId {CorrelationId}; skipping (Hangfire retry).",
                correlationId);
            _dataContext.Entry(claim).State = EntityState.Detached;
            return;
        }

        if (await _staleFireGuard.IsStaleFireAsync(userId, "PickDeadline", pickemGroupId, seasonWeek, fireTimeUtc, waveAnchorUtc))
        {
            await FinalizeAsync(claim, "Suppressed_StaleFire");
            return;
        }

        // Prefs gate — projected to the one flag; null (no row) allows.
        var reminderEnabled = await _dataContext.UserNotificationPreferences
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => (bool?)p.PickDeadlineReminderEnabled)
            .FirstOrDefaultAsync();

        if (reminderEnabled == false)
        {
            await FinalizeAsync(claim, "Suppressed_UserOptedOut");
            return;
        }

        // Missing-pick gate — the whole point of the reminder. Wave
        // membership is recomputed from current DB state (kickoffs may
        // have shifted within the wave since scheduling): every matchup
        // in this league-week starting within the coalesce window of the
        // anchor. A pick on file suppresses that game; all picked (or
        // wave emptied by reschedules) → no push, audited.
        var waveEndUtc = waveAnchorUtc.AddMinutes(_config.PickDeadlineCoalesceMinutes);
        var waveMatchups = await _dataContext.PickemGroupMatchups
            .AsNoTracking()
            .Where(m => m.PickemGroupId == pickemGroupId
                        && m.SeasonWeek == seasonWeek
                        && m.StartDateUtc >= waveAnchorUtc
                        && m.StartDateUtc <= waveEndUtc)
            .Select(m => new { m.ContestId, m.Headline })
            .ToListAsync();

        if (waveMatchups.Count == 0)
        {
            await FinalizeAsync(claim, "Suppressed_NoMatchups");
            return;
        }

        var waveContestIds = waveMatchups.Select(m => m.ContestId).ToList();
        var pickedContestIds = await _dataContext.UserPicks
            .AsNoTracking()
            .Where(p => p.UserId == userId
                        && p.PickemGroupId == pickemGroupId
                        && waveContestIds.Contains(p.ContestId))
            .Select(p => p.ContestId)
            .ToListAsync();
        var picked = pickedContestIds.ToHashSet();
        var unpicked = waveMatchups.Where(m => !picked.Contains(m.ContestId)).ToList();

        if (unpicked.Count == 0)
        {
            await FinalizeAsync(claim, "Suppressed_AllPicked");
            return;
        }

        // Body composition — league name from the local projection.
        var leagueName = await _dataContext.PickemGroups
            .AsNoTracking()
            .Where(g => g.Id == pickemGroupId)
            .Select(g => g.Name)
            .FirstOrDefaultAsync();

        const string title = "Picks due soon";
        var body = ComposePickDeadlineBody(unpicked.Count,
            unpicked.Count == 1 ? unpicked[0].Headline : null, leagueName,
            FormatLeadPhrase(_config.PickDeadlineLeadMinutes));

        var outcome = await _fanout.SendToUserDevicesAsync(userId, title, body);

        if (outcome.Result == PushFanoutResult.NoDevices)
        {
            await FinalizeAsync(claim, "Suppressed_NoDevice");
            return;
        }

        claim.Title = title;
        claim.Body = body;
        claim.Result = outcome.Result == PushFanoutResult.Sent ? "Sent" : "Failed_FcmError";
        claim.FailureReason = outcome.FailureReason;
        claim.ModifiedUtc = _dateTimeProvider.UtcNow();

        await _dataContext.SaveChangesAsync();
    }

    /// <summary>
    /// Operator-approved copy (docs/features/pick-deadline-reminders-v2.md):
    /// one unpicked game names the matchup; several collapse to a count.
    /// A null headline on the single case falls back to count wording.
    /// The time phrase tracks the configured lead so retuning
    /// PickDeadlineLeadMinutes never makes the copy lie.
    /// </summary>
    private static string ComposePickDeadlineBody(
        int unpickedCount, string? singleHeadline, string? leagueName, string leadPhrase)
    {
        if (unpickedCount == 1 && !string.IsNullOrWhiteSpace(singleHeadline))
        {
            return leagueName is not null
                ? $"Your pick for {singleHeadline} ({leagueName}) locks in {leadPhrase}."
                : $"Your pick for {singleHeadline} locks in {leadPhrase}.";
        }

        var noun = unpickedCount == 1 ? "pick locks" : "picks lock";
        return leagueName is not null
            ? $"{unpickedCount} {noun} in {leadPhrase} in {leagueName}."
            : $"{unpickedCount} {noun} in {leadPhrase}.";
    }

    private static string FormatLeadPhrase(int leadMinutes) => leadMinutes switch
    {
        60 => "about an hour",
        _ => $"about {leadMinutes} minutes"
    };

    private async Task FinalizeAsync(NotificationPickDeadline claim, string result)
    {
        claim.Result = result;
        claim.ModifiedUtc = _dateTimeProvider.UtcNow();
        await _dataContext.SaveChangesAsync();
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        // Npgsql surfaces unique-violation as SQLSTATE 23505.
        return ex.InnerException is Npgsql.PostgresException pg && pg.SqlState == "23505";
    }
}
