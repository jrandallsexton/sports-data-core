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

    public UpsertUserCommandHandler(
        AppDataContext db,
        ILogger<UpsertUserCommandHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result<Guid>> ExecuteAsync(
        UpsertUserCommand command,
        string firebaseUid,
        string signInProvider,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = new List<FluentValidation.Results.ValidationFailure>();

        if (string.IsNullOrWhiteSpace(firebaseUid))
        {
            validationErrors.Add(new FluentValidation.Results.ValidationFailure(
                nameof(firebaseUid), "Firebase UID is required."));
        }

        if (string.IsNullOrWhiteSpace(command.Email))
        {
            validationErrors.Add(new FluentValidation.Results.ValidationFailure(
                nameof(command.Email), "Email is required."));
        }

        if (validationErrors.Count > 0)
        {
            _logger.LogWarning("Validation failed: {Errors}", string.Join(", ", validationErrors.Select(e => e.PropertyName)));
            return new Failure<Guid>(default, ResultStatus.BadRequest, validationErrors);
        }

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
            _logger.LogInformation("Updating last login for user: {FirebaseUid}", firebaseUid);

            user.Email = command.Email;
            user.SignInProvider = signInProvider;
            user.DisplayName = command.DisplayName ?? user.DisplayName;
            user.LastLoginUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User upserted successfully. UserId={UserId}", user.Id);

        return new Success<Guid>(user.Id);
    }
}
