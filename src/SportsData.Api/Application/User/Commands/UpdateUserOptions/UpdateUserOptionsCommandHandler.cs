using FluentValidation;
using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.User.Commands.UpdateUserOptions;

public interface IUpdateUserOptionsCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(
        Guid userId,
        UpdateUserOptionsCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Upserts the KNOWN user-option rows (see <c>UserOptionKeys</c>) from the
/// typed command. Rows for keys this build doesn't know are left untouched —
/// a stale client can't clobber options added later. No eventing: options are
/// consumed by clients only (unlike notification preferences, which project
/// to the Notification service). See docs/features/user-options.md.
/// </summary>
public class UpdateUserOptionsCommandHandler : IUpdateUserOptionsCommandHandler
{
    private readonly AppDataContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly IValidator<UpdateUserOptionsCommand> _validator;
    private readonly ILogger<UpdateUserOptionsCommandHandler> _logger;

    public UpdateUserOptionsCommandHandler(
        AppDataContext db,
        IDateTimeProvider clock,
        IValidator<UpdateUserOptionsCommand> validator,
        ILogger<UpdateUserOptionsCommandHandler> logger)
    {
        _db = db;
        _clock = clock;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<Guid>> ExecuteAsync(
        Guid userId,
        UpdateUserOptionsCommand command,
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

        // The known keys this build writes. Future options append here.
        var desired = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [UserOptionKeys.ShowGamblingContent] = command.ShowGamblingContent.ToString()
        };

        var keys = desired.Keys.ToList();
        var existing = await _db.UserOptions
            .Where(o => o.UserId == userId && keys.Contains(o.Key))
            .ToListAsync(cancellationToken);

        var inserted = new List<UserOption>();
        foreach (var (key, value) in desired)
        {
            var row = existing.FirstOrDefault(o => o.Key == key);
            if (row is null)
            {
                row = new UserOption
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Key = key,
                    Value = value,
                    CreatedUtc = now,
                    CreatedBy = userId
                };
                await _db.UserOptions.AddAsync(row, cancellationToken);
                inserted.Add(row);
            }
            else if (row.Value != value)
            {
                row.Value = value;
                row.ModifiedUtc = now;
                row.ModifiedBy = userId;
            }
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (inserted.Count > 0 && ex.IsUniqueConstraintViolation())
        {
            // Race: a concurrent first-time request inserted one of this user's
            // option rows between our read and SaveChanges; the (UserId, Key)
            // unique index rejects ours. Detach the orphans, re-read the
            // winners, apply the requested values, retry as updates. Mirrors
            // UpdateNotificationPreferencesCommandHandler.
            _logger.LogWarning(ex,
                "Concurrent insert of user options. UserId={UserId}. Retrying as update.", userId);

            foreach (var orphan in inserted)
            {
                _db.Entry(orphan).State = EntityState.Detached;
            }

            var current = await _db.UserOptions
                .Where(o => o.UserId == userId && keys.Contains(o.Key))
                .ToListAsync(cancellationToken);

            foreach (var (key, value) in desired)
            {
                var row = current.FirstOrDefault(o => o.Key == key);
                if (row is null)
                {
                    // Still absent (the racing insert was for a different key):
                    // re-add ours.
                    await _db.UserOptions.AddAsync(new UserOption
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        Key = key,
                        Value = value,
                        CreatedUtc = now,
                        CreatedBy = userId
                    }, cancellationToken);
                }
                else if (row.Value != value)
                {
                    row.Value = value;
                    row.ModifiedUtc = now;
                    row.ModifiedBy = userId;
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("User options updated. UserId={UserId}", userId);

        return new Success<Guid>(userId);
    }
}
