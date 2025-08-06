using FirebaseAdmin.Auth;
using SportsData.Api.Application.User;

namespace SportsData.Api.Middleware;

public class FirebaseAuthenticationMiddleware
{
    private readonly RequestDelegate _next;

    public FirebaseAuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context, IUserService userService)
    {
        var token = context.Request.Headers["Authorization"].ToString()?.Replace("Bearer ", "");

        if (!string.IsNullOrEmpty(token))
        {
            FirebaseToken decodedToken;
            try
            {
                decodedToken = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(token);
            }
            catch
            {
                context.Response.StatusCode = 401;
                return;
            }

            var firebaseUid = decodedToken.Uid;
            var user = await userService.GetUserByFirebaseUidAsync(firebaseUid);

            if (user == null)
            {
                // Optional: auto-create
                user = await userService.GetOrCreateUserAsync(
                    firebaseUid,
                    decodedToken.Claims.TryGetValue("email", out var emailObj) ? emailObj?.ToString() ?? "unknown@example.com" : "unknown@example.com",
                    decodedToken.Claims.TryGetValue("name", out var nameObj) ? nameObj?.ToString() : null,
                    decodedToken.Claims.TryGetValue("picture", out var photoUrlObj) ? photoUrlObj?.ToString() : null,
                    decodedToken.Claims.TryGetValue("firebase", out var firebaseObj) && firebaseObj is Dictionary<string, object> firebaseDict && firebaseDict.TryGetValue("sign_in_provider", out var providerObj)
                        ? providerObj?.ToString() ?? "unknown"
                        : "unknown",
                    decodedToken.Claims.TryGetValue("email_verified", out var verifiedObj) && verifiedObj is bool emailVerified
                        ? emailVerified
                        : false
                );

            }

            context.Items["User"] = user;
        }

        await _next(context);
    }
}