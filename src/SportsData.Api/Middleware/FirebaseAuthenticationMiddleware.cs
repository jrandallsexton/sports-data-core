using FirebaseAdmin.Auth;
using Microsoft.Extensions.Caching.Memory;
using SportsData.Api.Application.User;
using System.Security.Claims;

namespace SportsData.Api.Middleware;

public class FirebaseAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;
    private readonly ILogger<FirebaseAuthenticationMiddleware> _logger;

    public FirebaseAuthenticationMiddleware(
        RequestDelegate next,
        IMemoryCache cache,
        ILogger<FirebaseAuthenticationMiddleware> logger)
    {
        _next = next;
        _cache = cache;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context, IUserService userService)
    {
        // If JWT Bearer already authenticated the user, enhance with database claims
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var firebaseUid = context.User.FindFirst("user_id")?.Value;
            if (!string.IsNullOrEmpty(firebaseUid))
            {
                await EnhanceAuthenticatedUser(context, userService, firebaseUid);
                await _next(context);
                return;
            }
        }

        // Otherwise, try to authenticate manually
        var token = context.Request.Headers["Authorization"].ToString()?.Replace("Bearer ", "");
        
        if (string.IsNullOrEmpty(token))
        {
            token = context.Request.Cookies["authToken"];
        }

        if (!string.IsNullOrEmpty(token))
        {
            FirebaseToken decodedToken;
            try
            {
                decodedToken = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(token);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Firebase token validation failed");
                context.Response.StatusCode = 401;
                return;
            }

            var firebaseUid = decodedToken.Uid;
            var cacheKey = $"user:{firebaseUid}";

            // ✅ Try to get from cache first - only hit DB on cache miss
            var user = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
                entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                
                _logger.LogInformation("Cache miss - fetching user from database: {FirebaseUid}", firebaseUid);

                var dbUser = await userService.GetUserByFirebaseUidAsync(firebaseUid);

                if (dbUser == null)
                {
                    _logger.LogInformation("User not found, creating new user: {FirebaseUid}", firebaseUid);
                    dbUser = await userService.GetOrCreateUserAsync(
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

                return dbUser;
            });

            // ✅ Defensive check - user should never be null but handle it
            if (user == null)
            {
                // Remove from cache to avoid caching transient failures
                _cache.Remove(cacheKey);
                _logger.LogError("CRITICAL: Failed to create or retrieve user after token validation. FirebaseUid: {FirebaseUid}. Cache entry removed.", firebaseUid);
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { error = "User authentication failed" });
                return;
            }

            // ✅ Add claims to ClaimsPrincipal for ASP.NET Core authorization
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim("firebase_uid", user.FirebaseUid),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("display_name", user.DisplayName ?? "Unknown")
            };

            // Add role-based claims
            if (user.IsAdmin)
            {
                claims.Add(new Claim(ClaimTypes.Role, "Admin"));
            }

            if (user.IsReadOnly)
            {
                claims.Add(new Claim("permission", "ReadOnly"));
            }

            if (user.IsSynthetic)
            {
                claims.Add(new Claim("user_type", "Synthetic"));
            }

            if (user.IsPanelPersona)
            {
                claims.Add(new Claim("user_type", "PanelPersona"));
            }

            // Merge with existing JWT claims from Firebase
            if (context.User.Identity is ClaimsIdentity existingIdentity)
            {
                // Don't duplicate NameIdentifier
                claims.AddRange(existingIdentity.Claims.Where(c => 
                    c.Type != ClaimTypes.NameIdentifier && 
                    c.Type != ClaimTypes.Email));
            }

            var identity = new ClaimsIdentity(claims, "FirebaseAuth");
            context.User = new ClaimsPrincipal(identity);

            // Store in Items for backward compatibility with existing code
            context.Items["User"] = user;
        }

        await _next(context);
    }

    private async Task EnhanceAuthenticatedUser(HttpContext context, IUserService userService, string firebaseUid)
    {
        var cacheKey = $"user:{firebaseUid}";

        var user = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
            entry.SlidingExpiration = TimeSpan.FromMinutes(5);
            
            var dbUser = await userService.GetUserByFirebaseUidAsync(firebaseUid);
            
            if (dbUser == null)
            {
                _logger.LogInformation("User not found in EnhanceAuthenticatedUser, creating new user: {FirebaseUid}", firebaseUid);
                
                // Extract claims from the existing JWT token
                var email = context.User.FindFirst(ClaimTypes.Email)?.Value 
                    ?? context.User.FindFirst("email")?.Value 
                    ?? "unknown@example.com";
                var name = context.User.FindFirst("name")?.Value;
                var picture = context.User.FindFirst("picture")?.Value;
                var provider = context.User.FindFirst("firebase")?.Value 
                    ?? context.User.FindFirst("sign_in_provider")?.Value 
                    ?? "unknown";
                var emailVerifiedClaim = context.User.FindFirst("email_verified")?.Value;
                var emailVerified = emailVerifiedClaim != null && bool.TryParse(emailVerifiedClaim, out var verified) && verified;
                
                dbUser = await userService.GetOrCreateUserAsync(
                    firebaseUid,
                    email,
                    name,
                    picture,
                    provider,
                    emailVerified
                );
            }
            
            return dbUser;
        });

        if (user == null)
        {
            // Remove from cache to avoid caching transient failures
            _cache.Remove(cacheKey);
            _logger.LogError("CRITICAL: Failed to create or retrieve user for FirebaseUid: {FirebaseUid}. Cache entry removed. Returning 401.", firebaseUid);
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "User authentication failed" });
            return;
        }

        // Add database-driven claims to the existing identity
        if (context.User.Identity is ClaimsIdentity identity)
        {
            // Add role claims
            if (user.IsAdmin && !context.User.IsInRole("Admin"))
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, "Admin"));
            }

            if (user.IsReadOnly)
            {
                identity.AddClaim(new Claim("permission", "ReadOnly"));
            }

            if (user.IsSynthetic)
            {
                identity.AddClaim(new Claim("user_type", "Synthetic"));
            }

            if (user.IsPanelPersona)
            {
                identity.AddClaim(new Claim("user_type", "PanelPersona"));
            }

            // Store in Items for backward compatibility
            context.Items["User"] = user;
        }
        else
        {
            // Identity is not ClaimsIdentity - this should never happen but handle it defensively
            _logger.LogError("CRITICAL: User identity is not ClaimsIdentity for FirebaseUid: {FirebaseUid}. Returning 401.", firebaseUid);
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid identity type" });
            return;
        }
    }
}