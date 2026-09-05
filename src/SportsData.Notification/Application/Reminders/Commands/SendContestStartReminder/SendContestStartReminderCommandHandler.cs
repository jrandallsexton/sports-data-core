#nullable enable

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Notification.Application.Dispatching;
using SportsData.Notification.Infrastructure.Data;
using SportsData.Notification.Infrastructure.Data.Entities;

namespace SportsData.Notification.Application.Reminders.Commands.SendContestStartReminder;

public interface ISendContestStartReminderCommandHandler
{
    /// <summary>
    /// Hangfire-invoked contest-start dispatch: per-contest scope,
    /// sport-aware copy resolved at fire time.
    /// </summary>
    Task ExecuteAsync(Guid userId, Guid contestId, DateTime fireTimeUtc);
}

/// <summary>
/// Atomic-claim + dispatch for the contest-start reminder. Idempotency
/// rides on <c>NotificationContestStart (UserId, ContestId, FireTimeUtc)</c>
/// — same fire-time versioning as the pick-deadline handler.
/// </summary>
public class SendContestStartReminderCommandHandler : ISendContestStartReminderCommandHandler
{
    private readonly ILogger<SendContestStartReminderCommandHandler> _logger;
    private readonly AppDataContext _dataContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IStaleFireGuard _staleFireGuard;
    private readonly IPushDeviceFanout _fanout;

    public SendContestStartReminderCommandHandler(
        ILogger<SendContestStartReminderCommandHandler> logger,
        AppDataContext dataContext,
        IDateTimeProvider dateTimeProvider,
        IStaleFireGuard staleFireGuard,
        IPushDeviceFanout fanout)
    {
        _logger = logger;
        _dataContext = dataContext;
        _dateTimeProvider = dateTimeProvider;
        _staleFireGuard = staleFireGuard;
        _fanout = fanout;
    }

    public async Task ExecuteAsync(Guid userId, Guid contestId, DateTime fireTimeUtc)
    {
        // fireTimeUtc IS the version anchor — what the scheduler intended
        // this job to fire at. Used both for the deterministic trace id and
        // for the stale-fire check below.
        var correlationId = ReminderCorrelation.DeterministicCorrelationId(
            "ContestStart", userId, contestId, fireTimeUtc.Ticks);

        using var _ = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["UserId"] = userId,
            ["ContestId"] = contestId,
            ["FireTimeUtc"] = fireTimeUtc
        });

        _logger.LogInformation("SendContestStartReminder invoked.");

        // Atomic claim on (UserId, ContestId, FireTimeUtc) — natural-key
        // dedup with fire-time versioning, same as PickDeadline.
        var claim = new NotificationContestStart
        {
            UserId = userId,
            ContestId = contestId,
            FireTimeUtc = fireTimeUtc,
            CorrelationId = correlationId,
            Channel = "Fcm",
            Result = "Dispatching",
            AttemptedUtc = _dateTimeProvider.UtcNow()
        };
        _dataContext.NotificationContestStarts.Add(claim);

        try
        {
            await _dataContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            _logger.LogInformation(
                "ContestStart reminder already dispatched for CorrelationId {CorrelationId}; skipping (Hangfire retry).",
                correlationId);
            _dataContext.Entry(claim).State = EntityState.Detached;
            return;
        }

        // ContestStart rows leave SeasonWeek + WaveAnchorUtc null in the
        // PendingScheduledJob natural key.
        if (await _staleFireGuard.IsStaleFireAsync(userId, "ContestStart", contestId, seasonWeek: null, fireTimeUtc))
        {
            await FinalizeAsync(claim, "Suppressed_StaleFire");
            return;
        }

        // Prefs gate — projected to the one flag; null (no row) allows.
        var reminderEnabled = await _dataContext.UserNotificationPreferences
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => (bool?)p.ContestStartReminderEnabled)
            .FirstOrDefaultAsync();

        if (reminderEnabled == false)
        {
            await FinalizeAsync(claim, "Suppressed_UserOptedOut");
            return;
        }

        // Resolve sport for the user-facing copy. A Contest is sport-
        // specific, so every PickemGroup containing it shares one Sport
        // value — any matchup row's PickemGroup is authoritative. Default
        // to Sport.All when none found (defensive: sport lookup races
        // membership deletion); the terminology helper falls back to
        // generic "Game starting soon" copy in that case.
        var sport = await _dataContext.PickemGroupMatchups
            .AsNoTracking()
            .Where(m => m.ContestId == contestId)
            .Join(_dataContext.PickemGroups,
                m => m.PickemGroupId,
                g => g.Id,
                (m, g) => g.Sport)
            .FirstOrDefaultAsync();

        var (title, body) = SportTerminology.GetContestStartCopy(sport);

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

    private async Task FinalizeAsync(NotificationContestStart claim, string result)
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
