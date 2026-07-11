using FluentValidation;
using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Users;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.User.Commands.UpdateNotificationPreferences;

public interface IUpdateNotificationPreferencesCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(
        Guid userId,
        UpdateNotificationPreferencesCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Persists a user's per-category notification opt-in flags (canonical owner is
/// the API) and publishes <see cref="UserNotificationPreferencesUpdated"/> so the
/// Notification service projects the new values into the table its dispatchers
/// read. First change creates the row; later changes overwrite it in place.
/// See docs/mobile/notification-preferences.md.
/// </summary>
public class UpdateNotificationPreferencesCommandHandler : IUpdateNotificationPreferencesCommandHandler
{
    private readonly AppDataContext _db;
    private readonly IEventBus _eventBus;
    private readonly IDateTimeProvider _clock;
    private readonly IValidator<UpdateNotificationPreferencesCommand> _validator;
    private readonly ILogger<UpdateNotificationPreferencesCommandHandler> _logger;

    public UpdateNotificationPreferencesCommandHandler(
        AppDataContext db,
        IEventBus eventBus,
        IDateTimeProvider clock,
        IValidator<UpdateNotificationPreferencesCommand> validator,
        ILogger<UpdateNotificationPreferencesCommandHandler> logger)
    {
        _db = db;
        _eventBus = eventBus;
        _clock = clock;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<Guid>> ExecuteAsync(
        Guid userId,
        UpdateNotificationPreferencesCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return new Failure<Guid>(default!, ResultStatus.BadRequest, validation.Errors);
        }

        var userExists = await _db.Users.AnyAsync(u => u.Id == userId, cancellationToken);
        if (!userExists)
        {
            return new Failure<Guid>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(userId), $"User with ID {userId} not found.")]);
        }

        var now = _clock.UtcNow();

        var prefs = await _db.UserNotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        var isInsert = prefs is null;
        if (isInsert)
        {
            prefs = new UserNotificationPreferences
            {
                UserId = userId,
                CreatedUtc = now,
                CreatedBy = userId
            };
            Apply(prefs, command);
            await _db.UserNotificationPreferences.AddAsync(prefs, cancellationToken);
        }
        else
        {
            Apply(prefs!, command);
            prefs!.ModifiedUtc = now;
            prefs.ModifiedBy = userId;
        }

        // Publish before SaveChanges so the outbox row persists atomically with the
        // preference write (EF bus outbox flushes on save).
        await _eventBus.Publish(
            new UserNotificationPreferencesUpdated(
                userId,
                command.PickResultEnabled,
                command.PickDeadlineReminderEnabled,
                command.ContestStartReminderEnabled,
                command.LeagueInviteEnabled,
                command.MembershipEnabled,
                command.MatchupPreviewEnabled,
                command.ScheduleChangeEnabled,
                command.OddsChangedEnabled,
                Guid.NewGuid(),
                Guid.NewGuid()),
            cancellationToken);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (isInsert && ex.IsUniqueConstraintViolation())
        {
            // Race: a concurrent first-time request inserted this user's row between
            // our FirstOrDefault check and SaveChanges. The unique index on UserId
            // rejects our insert. Detach the orphan, re-query the winner's row, apply
            // the requested flags, and retry as an update. The outbox message stays
            // tracked (Added), so the event still fires exactly once on the retry.
            _logger.LogWarning(ex,
                "Concurrent insert of notification preferences. UserId={UserId}. Retrying as update.", userId);

            _db.Entry(prefs!).State = EntityState.Detached;

            var existing = await _db.UserNotificationPreferences
                .FirstAsync(p => p.UserId == userId, cancellationToken);
            Apply(existing, command);
            existing.ModifiedUtc = now;
            existing.ModifiedBy = userId;

            await _db.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Notification preferences updated. UserId={UserId}", userId);

        return new Success<Guid>(userId);
    }

    private static void Apply(UserNotificationPreferences prefs, UpdateNotificationPreferencesCommand command)
    {
        prefs.PickResultEnabled = command.PickResultEnabled;
        prefs.PickDeadlineReminderEnabled = command.PickDeadlineReminderEnabled;
        prefs.ContestStartReminderEnabled = command.ContestStartReminderEnabled;
        prefs.LeagueInviteEnabled = command.LeagueInviteEnabled;
        prefs.MembershipEnabled = command.MembershipEnabled;
        prefs.MatchupPreviewEnabled = command.MatchupPreviewEnabled;
        prefs.ScheduleChangeEnabled = command.ScheduleChangeEnabled;
        prefs.OddsChangedEnabled = command.OddsChangedEnabled;
    }
}
