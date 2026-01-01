using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.User.Commands.UpsertUser;
using SportsData.Api.Application.User.Dtos;
using SportsData.Api.Application.User.Queries.GetMe;
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
    private readonly IUpsertUserCommandHandler _upsertUserHandler;
    private readonly IGetMeQueryHandler _getMeHandler;

    public UserService(
        AppDataContext db,
        ILogger<UserService> logger,
        IUpsertUserCommandHandler upsertUserHandler,
        IGetMeQueryHandler getMeHandler)
    {
        _db = db;
        _logger = logger;
        _upsertUserHandler = upsertUserHandler;
        _getMeHandler = getMeHandler;
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
        var query = new GetMeQuery { UserId = id };
        var result = await _getMeHandler.ExecuteAsync(query);

        if (!result.IsSuccess)
            throw new KeyNotFoundException($"User with ID {id} not found.");

        return result.Value;
    }

    public async Task<Infrastructure.Data.Entities.User> GetOrCreateUserAsync(
        string firebaseUid,
        string email,
        string? displayName,
        string? photoUrl,
        string? signInProvider,
        bool emailVerified)
    {
        var command = new UpsertUserCommand
        {
            Email = email,
            DisplayName = displayName
        };

        var result = await _upsertUserHandler.ExecuteAsync(
            command,
            firebaseUid,
            signInProvider ?? "unknown");

        if (!result.IsSuccess)
        {
            _logger.LogError("Failed to upsert user: {FirebaseUid}", firebaseUid);
            throw new InvalidOperationException("Failed to create or update user.");
        }

        // Need to return the actual entity, so fetch it
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == result.Value);

        if (user == null)
            throw new InvalidOperationException("User was created but could not be retrieved.");

        return user;
    }
}
