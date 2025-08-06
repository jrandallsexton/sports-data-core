using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;

namespace SportsData.Api.Application.User;

public interface IUserService
{
    Task<Infrastructure.Data.Entities.User> GetOrCreateUserAsync(
        string firebaseUid,
        string email,
        string? displayName,
        string? photoUrl,
        string signInProvider,
        bool emailVerified);

    Task<Infrastructure.Data.Entities.User?> GetUserByFirebaseUidAsync(string firebaseUid);

    Task<UserDto> GetUserDtoById(Guid id);
}

public class UserService : IUserService
{
    private readonly AppDataContext _db;
    private readonly ILogger<UserService> _logger;

    public UserService(AppDataContext db, ILogger<UserService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Infrastructure.Data.Entities.User?> GetUserByFirebaseUidAsync(string firebaseUid)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.FirebaseUid == firebaseUid);

        if (user == null)
            return null;

        return user;
    }

    public async Task<UserDto> GetUserDtoById(Guid id)
    {
        var user = await _db.Users
            .Include(x => x.GroupMemberships)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (user is null)
            throw new KeyNotFoundException($"User with ID {id} not found.");

        return new UserDto
        {
            Id = user.Id,
            FirebaseUid = user.FirebaseUid,
            Email = user.Email,
            DisplayName = user.DisplayName,
            LastLoginUtc = user.LastLoginUtc,
            HasLeagues = user.GroupMemberships.Any()
        };
    }

    public async Task<Infrastructure.Data.Entities.User> GetOrCreateUserAsync(string firebaseUid, string email, string? displayName, string? photoUrl, string? signInProvider, bool emailVerified)
    {
        if (string.IsNullOrWhiteSpace(firebaseUid) || string.IsNullOrWhiteSpace(email))
        {
            _logger.LogWarning("Missing Firebase UID or Email. Cannot continue.");
            throw new ArgumentException("Firebase UID and Email are required.");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.FirebaseUid == firebaseUid);

        if (user == null)
        {
            _logger.LogInformation("Creating new user: {FirebaseUid}", firebaseUid);

            user = new Infrastructure.Data.Entities.User
            {
                Id = Guid.NewGuid(),
                FirebaseUid = firebaseUid,
                Email = email,
                EmailVerified = emailVerified,
                SignInProvider = signInProvider ?? "unknown",
                DisplayName = displayName,
                LastLoginUtc = DateTime.UtcNow,
                CreatedUtc = DateTime.UtcNow
            };

            _db.Users.Add(user);
        }
        else
        {
            _logger.LogInformation("Updating last login for user: {FirebaseUid}", firebaseUid);

            user.Email = email;
            user.EmailVerified = emailVerified;
            user.SignInProvider = signInProvider ?? user.SignInProvider;
            user.DisplayName = displayName ?? user.DisplayName;
            user.LastLoginUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        return user;
    }
}
