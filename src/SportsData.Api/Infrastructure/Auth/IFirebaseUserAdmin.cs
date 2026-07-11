namespace SportsData.Api.Infrastructure.Auth;

/// <summary>
/// Thin, injectable wrapper over Firebase Admin user management so command
/// handlers can be unit-tested without the static <c>FirebaseAuth.DefaultInstance</c>.
/// </summary>
public interface IFirebaseUserAdmin
{
    /// <summary>
    /// Deletes the Firebase auth user (removes the login). Idempotent: a
    /// no-longer-existing uid is treated as success.
    /// </summary>
    Task DeleteUserAsync(string firebaseUid, CancellationToken cancellationToken = default);
}
