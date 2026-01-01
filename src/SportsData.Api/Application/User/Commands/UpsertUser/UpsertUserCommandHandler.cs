using FluentValidation;
using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

namespace SportsData.Api.Application.User.Commands.UpsertUser;

public interface IUpsertUserCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(
        UpsertUserCommand command,
        string firebaseUid,
        string signInProvider,
        CancellationToken cancellationToken = default);
}

public class UpsertUserCommandHandler : IUpsertUserCommandHandler
{
    private readonly AppDataContext _db;
    private readonly ILogger<UpsertUserCommandHandler> _logger;
    private readonly IValidator<UpsertUserCommand> _validator;

    public UpsertUserCommandHandler(
        AppDataContext db,
        ILogger<UpsertUserCommandHandler> logger,
        IValidator<UpsertUserCommand> validator)
    {
        _db = db;
        _logger = logger;
        _validator = validator;
    }

    public async Task<Result<Guid>> ExecuteAsync(
        UpsertUserCommand command,
        string firebaseUid,
        string signInProvider,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = new List<FluentValidation.Results.ValidationFailure>();

        // Validate firebaseUid parameter
        if (string.IsNullOrWhiteSpace(firebaseUid))
        {
            validationErrors.Add(new FluentValidation.Results.ValidationFailure(
                "FirebaseUid", "Firebase UID is required."));
        }

        // Validate command using FluentValidation
        var commandValidationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!commandValidationResult.IsValid)
        {
            validationErrors.AddRange(commandValidationResult.Errors);
        }

        if (validationErrors.Count > 0)
        {
            _logger.LogWarning("Validation failed: {Errors}", string.Join(", ", validationErrors.Select(e => e.PropertyName)));
            return new Failure<Guid>(default, ResultStatus.BadRequest, validationErrors);
        }

        try
        {
            return await UpsertUserInternalAsync(command, firebaseUid, signInProvider, cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Race condition: another request created the user between our check and insert
            _logger.LogWarning(ex, 
                "Unique constraint violation when creating user {FirebaseUid}. Another request likely created this user concurrently. Retrying...", 
                firebaseUid);

            // Retry once by fetching the existing user
            var existingUser = await _db.Users
                .FirstOrDefaultAsync(u => u.FirebaseUid == firebaseUid, cancellationToken);

            if (existingUser != null)
            {
                _logger.LogInformation("Found existing user after constraint violation. UserId={UserId}", existingUser.Id);
                
                // Update the existing user
                // If email changes, reset email verification status
                if (!string.Equals(existingUser.Email, command.Email, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Email changed for user {UserId} from {OldEmail} to {NewEmail}. Resetting EmailVerified to false.", 
                        existingUser.Id, existingUser.Email, command.Email);
                    existingUser.EmailVerified = false;
                }

                existingUser.Email = command.Email;
                existingUser.SignInProvider = signInProvider;
                existingUser.DisplayName = command.DisplayName ?? existingUser.DisplayName;
                existingUser.LastLoginUtc = DateTime.UtcNow;

                try
                {
                    await _db.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException saveEx)
                {
                    _logger.LogError(saveEx, 
                        "Database update error when saving user changes after constraint violation. UserId={UserId}, FirebaseUid={FirebaseUid}", 
                        existingUser.Id, firebaseUid);
                    return new Failure<Guid>(
                        default,
                        ResultStatus.Error,
                        [new FluentValidation.Results.ValidationFailure("Database", "Failed to save user changes. Please try again.")]);
                }
                catch (Exception saveEx)
                {
                    _logger.LogError(saveEx, 
                        "Unexpected error when saving user changes after constraint violation. UserId={UserId}, FirebaseUid={FirebaseUid}", 
                        existingUser.Id, firebaseUid);
                    return new Failure<Guid>(
                        default,
                        ResultStatus.Error,
                        [new FluentValidation.Results.ValidationFailure("System", "An unexpected error occurred. Please try again.")]);
                }
                
                return new Success<Guid>(existingUser.Id);
            }

            // If we still can't find the user, something is very wrong
            _logger.LogError(ex, "Unique constraint violation but user not found. FirebaseUid={FirebaseUid}", firebaseUid);
            throw;
        }
    }

    private async Task<Result<Guid>> UpsertUserInternalAsync(
        UpsertUserCommand command,
        string firebaseUid,
        string signInProvider,
        CancellationToken cancellationToken)
    {
        var user = await _db.Users.FirstOrDefaultAsync(
            u => u.FirebaseUid == firebaseUid,
            cancellationToken);

        if (user == null)
        {
            _logger.LogInformation("Creating new user: {FirebaseUid}", firebaseUid);

            user = new Infrastructure.Data.Entities.User
            {
                Id = Guid.NewGuid(),
                FirebaseUid = firebaseUid,
                Email = command.Email,
                EmailVerified = false,
                SignInProvider = signInProvider,
                DisplayName = command.DisplayName ?? DisplayNameGenerator.Generate(),
                LastLoginUtc = DateTime.UtcNow,
                CreatedUtc = DateTime.UtcNow
            };

            _db.Users.Add(user);
        }
        else
        {
            _logger.LogInformation("Updating user: {FirebaseUid}", firebaseUid);

            // If email changes, reset email verification status
            if (!string.Equals(user.Email, command.Email, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Email changed for user {UserId} from {OldEmail} to {NewEmail}. Resetting EmailVerified to false.", 
                    user.Id, user.Email, command.Email);
                user.EmailVerified = false;
            }

            user.Email = command.Email;
            user.SignInProvider = signInProvider;
            user.DisplayName = command.DisplayName ?? user.DisplayName;
            user.LastLoginUtc = DateTime.UtcNow;
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(dbEx, 
                "Database update error when upserting user. FirebaseUid={FirebaseUid}, Email={Email}", 
                firebaseUid, command.Email);
            return new Failure<Guid>(
                default,
                ResultStatus.Error,
                [new FluentValidation.Results.ValidationFailure("Database", "Failed to save user. Please try again.")]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Unexpected error when upserting user. FirebaseUid={FirebaseUid}, Email={Email}", 
                firebaseUid, command.Email);
            return new Failure<Guid>(
                default,
                ResultStatus.Error,
                [new FluentValidation.Results.ValidationFailure("System", "An unexpected error occurred. Please try again.")]);
        }

        _logger.LogInformation("User upserted successfully. UserId={UserId}", user.Id);

        return new Success<Guid>(user.Id);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        // Check for PostgreSQL unique constraint violation
        // Error code 23505 is for unique_violation in PostgreSQL
        if (ex.InnerException?.Message.Contains("23505") == true ||
            ex.InnerException?.Message.Contains("duplicate key") == true ||
            ex.InnerException?.Message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        // Check for SQL Server unique constraint violation
        // Error number 2601 is for duplicate key in SQL Server
        if (ex.InnerException?.Message.Contains("2601") == true ||
            ex.InnerException?.Message.Contains("2627") == true ||
            ex.InnerException?.Message.Contains("Cannot insert duplicate key", StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        return false;
    }
}
