using System.Security.Claims;

using FluentAssertions;

using SportsData.Api.Middleware;

using Xunit;

namespace SportsData.Api.Tests.Unit.Middleware;

/// <summary>
/// Regression coverage for the provisioning outage: the JWT Bearer handler
/// surfaces the token's nested <c>firebase</c> object as one claim holding raw
/// JSON. Storing that blob overflowed User.SignInProvider (varchar 100) for
/// federated identities, so every NEW Google user failed to provision and was
/// locked out of the API. The payloads below are the real shapes observed in
/// production.
/// </summary>
public class FirebaseSignInProviderResolverTests
{
    // Verbatim shape of a Google identity's `firebase` claim — 115+ chars,
    // which is what overflowed the column.
    private const string GoogleClaimJson =
        """{"identities":{"google.com":["109384756102938475610"],"email":["someone@gmail.com"]},"sign_in_provider":"google.com"}""";

    // Email/password shape. This one squeaked under 100 chars, which is why
    // password users provisioned (with the blob stored as their "provider")
    // while Google users failed outright.
    private const string PasswordClaimJson =
        """{"identities":{"email":["user6@sportdeets.com"]},"sign_in_provider":"password"}""";

    private static ClaimsPrincipal PrincipalWith(params (string Type, string Value)[] claims) =>
        new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), "TestAuth"));

    [Fact]
    public void Resolve_ExtractsProvider_FromGoogleFirebaseClaimJson()
    {
        var principal = PrincipalWith(("firebase", GoogleClaimJson));

        var result = FirebaseSignInProviderResolver.Resolve(principal);

        result.Should().Be("google.com");
        result.Length.Should().BeLessThanOrEqualTo(FirebaseSignInProviderResolver.MaxLength,
            "an oversized value fails the INSERT and blocks user provisioning");
    }

    [Fact]
    public void Resolve_ExtractsProvider_FromPasswordFirebaseClaimJson()
    {
        var principal = PrincipalWith(("firebase", PasswordClaimJson));

        FirebaseSignInProviderResolver.Resolve(principal).Should().Be("password");
    }

    [Fact]
    public void Resolve_NeverReturnsRawJson_EvenWhenSignInProviderIsAbsent()
    {
        // A firebase object without sign_in_provider must NOT fall through to
        // "use the blob" — that is precisely the original defect.
        var principal = PrincipalWith(
            ("firebase", """{"identities":{"email":["a@b.com"]}}"""));

        var result = FirebaseSignInProviderResolver.Resolve(principal);

        result.Should().Be(FirebaseSignInProviderResolver.Unknown);
        result.Should().NotStartWith("{");
    }

    [Fact]
    public void Resolve_UsesFlattenedDottedClaim_WhenFirebaseObjectAbsent()
    {
        var principal = PrincipalWith(("firebase.sign_in_provider", "apple.com"));

        FirebaseSignInProviderResolver.Resolve(principal).Should().Be("apple.com");
    }

    [Fact]
    public void Resolve_UsesBareSignInProviderClaim_WhenFirebaseObjectAbsent()
    {
        var principal = PrincipalWith(("sign_in_provider", "password"));

        FirebaseSignInProviderResolver.Resolve(principal).Should().Be("password");
    }

    [Fact]
    public void Resolve_AcceptsBareProviderString_OnFirebaseClaim()
    {
        // Some pipelines flatten the claim to the provider itself.
        var principal = PrincipalWith(("firebase", "google.com"));

        FirebaseSignInProviderResolver.Resolve(principal).Should().Be("google.com");
    }

    [Fact]
    public void Resolve_ReturnsUnknown_WhenNoProviderClaimsPresent()
    {
        FirebaseSignInProviderResolver.Resolve(PrincipalWith(("email", "a@b.com")))
            .Should().Be(FirebaseSignInProviderResolver.Unknown);
    }

    [Theory]
    [InlineData(null, "unknown")]
    [InlineData("", "unknown")]
    [InlineData("   ", "unknown")]
    [InlineData(" google.com ", "google.com")]
    public void Normalize_CollapsesBlanksAndTrims(string? input, string expected)
    {
        FirebaseSignInProviderResolver.Normalize(input).Should().Be(expected);
    }

    [Fact]
    public void Normalize_ClampsToColumnWidth()
    {
        var oversized = new string('x', FirebaseSignInProviderResolver.MaxLength + 50);

        FirebaseSignInProviderResolver.Normalize(oversized)
            .Length.Should().Be(FirebaseSignInProviderResolver.MaxLength);
    }
}
