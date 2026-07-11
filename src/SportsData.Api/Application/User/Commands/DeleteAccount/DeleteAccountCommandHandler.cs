using FluentValidation;
using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Auth;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Users;

namespace SportsData.Api.Application.User.Commands.DeleteAccount;

public interface IDeleteAccountCommandHandler
{
    Task<Result<bool>> ExecuteAsync(DeleteAccountCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Deletes a user's account (App Store 5.1.1(v) / GDPR). Anonymizes the
/// canonical <c>User</c> — PII stripped, login removed — while retaining game
/// history so co-players' leagues stay intact, then publishes
/// <see cref="UserDeleted"/> so the Notification service purges the user's
/// devices/preferences/scheduled jobs. See docs/mobile/account-deletion.md.
/// </summary>
public class DeleteAccountCommandHandler : IDeleteAccountCommandHandler
{
    private readonly AppDataContext _db;
    private readonly IFirebaseUserAdmin _firebase;
    private readonly IEventBus _eventBus;
    private readonly IDateTimeProvider _clock;
    private readonly IValidator<DeleteAccountCommand> _validator;
    private readonly ILogger<DeleteAccountCommandHandler> _logger;

    public DeleteAccountCommandHandler(
        AppDataContext db,
        IFirebaseUserAdmin firebase,
        IEventBus eventBus,
        IDateTimeProvider clock,
        IValidator<DeleteAccountCommand> validator,
        ILogger<DeleteAccountCommandHandler> logger)
    {
        _db = db;
        _firebase = firebase;
        _eventBus = eventBus;
        _clock = clock;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<bool>> ExecuteAsync(DeleteAccountCommand command, CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return new Failure<bool>(false, ResultStatus.BadRequest, validation.Errors);
        }

        var userId = command.UserId;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return new Failure<bool>(
                false,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(command.UserId), $"User with ID {userId} not found.")]);
        }

        // Idempotent: already deleted → nothing to do.
        if (user.DeletedUtc is not null)
        {
            return new Success<bool>(true);
        }

        // Kill the login first. If this throws (broker/network), we abort before
        // mutating any data. The wrapper treats an already-absent uid as success,
        // so a retried delete re-anonymizes cleanly.
        await _firebase.DeleteUserAsync(user.FirebaseUid, cancellationToken);

        // Anonymize in place — keep the row so picks/memberships/standings FKs
        // stay valid. Sentinels keep the unique indexes (FirebaseUid, Username)
        // satisfied.
        var idN = user.Id.ToString("N");
        user.FirebaseUid = $"deleted-{idN}";
        user.Email = $"deleted-{idN}@deleted.invalid";
        user.EmailVerified = false;
        user.Username = $"del_{idN}"[..30];
        user.DisplayName = "Deleted user";
        user.Timezone = null;
        user.DeletedUtc = _clock.UtcNow();

        // Publish before SaveChanges so the outbox row persists atomically with
        // the anonymization (EF bus outbox flushes on save).
        await _eventBus.Publish(
            new UserDeleted(user.Id, Guid.NewGuid(), Guid.NewGuid()),
            cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Account deleted (anonymized). UserId={UserId}", userId);

        return new Success<bool>(true);
    }
}
