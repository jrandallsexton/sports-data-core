using System.Security.Claims;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SportsData.Api.Tests.Integration.Fakes;

/// <summary>
/// Fake auth handler that acts as if a Firebase JWT was successfully validated.
/// The <c>user_id</c> claim is read by <c>FirebaseAuthenticationMiddleware</c>,
/// which then loads the matching DB user via <c>IUserService</c>. Tests must seed
/// a user row whose <c>FirebaseUid</c> matches <see cref="TestIdentity.FirebaseUid"/>.
/// </summary>
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestScheme";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim("user_id", TestIdentity.FirebaseUid),
            new Claim(ClaimTypes.NameIdentifier, TestIdentity.FirebaseUid),
            new Claim(ClaimTypes.Email, TestIdentity.Email),
            new Claim("email", TestIdentity.Email),
            new Claim("email_verified", "true"),
            new Claim("name", TestIdentity.DisplayName)
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public static class TestIdentity
{
    /// <summary>Stable Firebase UID used across the test suite. Match this on the seeded User row.</summary>
    public const string FirebaseUid = "test-firebase-uid-00000001";

    public const string Email = "integration@sportdeets.test";

    public const string DisplayName = "Integration Test User";
}
