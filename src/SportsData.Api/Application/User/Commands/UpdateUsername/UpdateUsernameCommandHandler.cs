using FluentValidation;
using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.User.Commands.UpdateUsername;

public interface IUpdateUsernameCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(
        Guid userId,
        UpdateUsernameCommand command,
        CancellationToken cancellationToken = default);
}

public class UpdateUsernameCommandHandler : IUpdateUsernameCommandHandler
{
    private readonly AppDataContext _db;
    private readonly ILogger<UpdateUsernameCommandHandler> _logger;
    private readonly IValidator<UpdateUsernameCommand> _validator;

    public UpdateUsernameCommandHandler(
        AppDataContext db,
        ILogger<UpdateUsernameCommandHandler> logger,
        IValidator<UpdateUsernameCommand> validator)
    {
        _db = db;
        _logger = logger;
        _validator = validator;
    }

    public async Task<Result<Guid>> ExecuteAsync(
        Guid userId,
        UpdateUsernameCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return new Failure<Guid>(default!, ResultStatus.BadRequest, validation.Errors);
        }

        // Store lowercased — the unique index is on the stored value, so
        // lowercasing here is what makes uniqueness case-insensitive.
        var normalized = UsernameNormalizer.Normalize(command.Username);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return new Failure<Guid>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(userId), $"User with ID {userId} not found.")]);
        }

        // No-op if unchanged (also lets the user re-submit their own handle).
        if (string.Equals(user.Username, normalized, StringComparison.Ordinal))
        {
            return new Success<Guid>(user.Id);
        }

        var taken = await _db.Users
            .AnyAsync(u => u.Username == normalized && u.Id != userId, cancellationToken);
        if (taken)
        {
            return new Failure<Guid>(
                default!,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(command.Username), "That username is already taken.")]);
        }

        user.Username = normalized;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            // Lost a race to another writer between the check and the save.
            return new Failure<Guid>(
                default!,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(command.Username), "That username is already taken.")]);
        }

        _logger.LogInformation("Username updated. UserId={UserId}, Username={Username}", user.Id, normalized);

        return new Success<Guid>(user.Id);
    }
}
