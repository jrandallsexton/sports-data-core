using System.Security.Claims;
using System.Text.Json;

namespace SportsData.Api.Middleware;

/// <summary>
/// Extracts the Firebase <c>sign_in_provider</c> ("google.com", "password",
/// "apple.com", ...) from a <see cref="ClaimsPrincipal"/> produced by the JWT
/// Bearer handler.
///
/// WHY THIS EXISTS: the Firebase ID token carries a nested <c>firebase</c>
/// object, and the JWT handler surfaces it as ONE claim whose value is the raw
/// JSON, e.g.
/// <code>
/// {"identities":{"google.com":["109..."],"email":["a@b.com"]},"sign_in_provider":"google.com"}
/// </code>
/// Reading <c>FindFirst("firebase").Value</c> therefore yields the whole blob,
/// not the provider. Persisting that blob overflowed
/// <c>User.SignInProvider</c> (varchar 100) for federated identities — the
/// email-only blob squeaked under the limit but the Google one did not — so
/// every NEW Google user failed to provision with
/// <c>22001: value too long</c> and was locked out of the API entirely.
/// Existing users were unaffected because provisioning only runs on a miss.
/// See docs/audit/firebase-signin-provider-overflow.md.
/// </summary>
public static class FirebaseSignInProviderResolver
{
    public const string Unknown = "unknown";

    /// <summary>Matches the User.SignInProvider column width.</summary>
    public const int MaxLength = 100;

    /// <summary>
    /// Resolves the sign-in provider, tolerating every claim shape the JWT
    /// pipeline can produce: the nested JSON object (normal), a flattened
    /// dotted claim, or a bare provider string. Returns <see cref="Unknown"/>
    /// rather than throwing — a token we can't classify must still be able to
    /// provision its user.
    /// </summary>
    public static string Resolve(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var firebaseClaim = principal.FindFirst("firebase")?.Value;

        if (!string.IsNullOrWhiteSpace(firebaseClaim))
        {
            var fromJson = ExtractFromJson(firebaseClaim);
            if (fromJson is not null)
                return fromJson;

            // Not JSON: some pipelines flatten the claim to the provider
            // itself. Accept it only if it's plausibly a provider name — never
            // let an unparsed object-looking value reach the database.
            var trimmed = firebaseClaim.Trim();
            if (!trimmed.StartsWith('{') && trimmed.Length <= MaxLength)
                return trimmed;
        }

        // Flattened variants emitted by some handler configurations.
        var flattened = principal.FindFirst("firebase.sign_in_provider")?.Value
                        ?? principal.FindFirst("sign_in_provider")?.Value;

        return Normalize(flattened);
    }

    /// <summary>
    /// Last line of defense before persistence: collapses null/blank to
    /// <see cref="Unknown"/> and clamps to the column width so a future claim
    /// shape can never again fail the INSERT and block user provisioning.
    /// </summary>
    public static string Normalize(string? signInProvider)
    {
        if (string.IsNullOrWhiteSpace(signInProvider))
            return Unknown;

        var trimmed = signInProvider.Trim();

        return trimmed.Length <= MaxLength
            ? trimmed
            : trimmed[..MaxLength];
    }

    private static string? ExtractFromJson(string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);

            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("sign_in_provider", out var provider) &&
                provider.ValueKind == JsonValueKind.String)
            {
                var parsed = provider.GetString();
                if (!string.IsNullOrWhiteSpace(parsed))
                    return parsed.Trim();
            }
        }
        catch (JsonException)
        {
            // Not JSON — the caller falls back to the bare-string handling.
        }

        return null;
    }
}
