using FluentValidation;
using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Users;

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

        if (prefs is null)
        {
            prefs = new UserNotificationPreferences
            {
                UserId = userId,
                CreatedUtc = now,
                CreatedBy = userId
            };
            await _db.UserNotificationPreferences.AddAsync(prefs, cancellationToken);
        }
        else
        {
            prefs.ModifiedUtc = now;
            prefs.ModifiedBy = userId;
        }

        prefs.PickResultEnabled = command.PickResultEnabled;
        prefs.PickDeadlineReminderEnabled = command.PickDeadlineReminderEnabled;
        prefs.ContestStartReminderEnabled = command.ContestStartReminderEnabled;
        prefs.LeagueInviteEnabled = command.LeagueInviteEnabled;
        prefs.MembershipEnabled = command.MembershipEnabled;
        prefs.MatchupPreviewEnabled = command.MatchupPreviewEnabled;
        prefs.ScheduleChangeEnabled = command.ScheduleChangeEnabled;
        prefs.OddsChangedEnabled = command.OddsChangedEnabled;

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

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Notification preferences updated. UserId={UserId}", userId);

        return new Success<Guid>(userId);
    }
}
