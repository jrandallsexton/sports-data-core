using FirebaseAdmin.Auth;

namespace SportsData.Api.Infrastructure.Auth;

/// <inheritdoc />
public class FirebaseUserAdmin : IFirebaseUserAdmin
{
    public async Task DeleteUserAsync(string firebaseUid, CancellationToken cancellationToken = default)
    {
        try
        {
            await FirebaseAuth.DefaultInstance.DeleteUserAsync(firebaseUid, cancellationToken);
        }
        catch (FirebaseAuthException ex) when (ex.AuthErrorCode == AuthErrorCode.UserNotFound)
        {
            // Already gone (e.g. a retried delete). Treat as success so the
            // caller can proceed to anonymize idempotently.
        }
    }
}
