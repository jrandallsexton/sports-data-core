using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SportsData.Notification.Infrastructure.Auth
{
    /// <summary>
    /// Per-endpoint API-key auth filter for Notification's admin / backfill
    /// surface. Reads the expected key from <c>CommonConfig:Notification:AdminApiKey</c>
    /// at request time so a key rotation through Azure App Config takes effect
    /// without a redeploy.
    ///
    /// <para>
    /// Intentionally minimal: header-only (<c>X-Api-Key</c>), constant-time
    /// comparison via <see cref="System.Security.Cryptography.CryptographicOperations.FixedTimeEquals"/>
    /// to avoid timing-side-channel inference of the expected key, 401 on
    /// missing/wrong, 500 if the server-side config slot is unset (refuse to
    /// auto-pass with an unconfigured key — better to fail loud than silently
    /// allow).
    /// </para>
    ///
    /// <para>
    /// Not a replacement for the JWT auth API uses for end-user routes.
    /// Admin / backfill endpoints in Notification are operator-triggered
    /// and don't have a user identity to authenticate against; an API key
    /// is the right shape.
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class ApiKeyAuthAttribute : Attribute, IAsyncAuthorizationFilter
    {
        public const string HeaderName = "X-Api-Key";
        public const string ConfigKey = "CommonConfig:Notification:AdminApiKey";

        public Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var providedKey)
                || string.IsNullOrWhiteSpace(providedKey))
            {
                context.Result = new UnauthorizedResult();
                return Task.CompletedTask;
            }

            var config = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var expectedKey = config[ConfigKey];

            if (string.IsNullOrWhiteSpace(expectedKey))
            {
                // Refuse to auto-pass when the server has no key configured.
                // Surfaces as 500 in logs so the operator knows the config is
                // missing rather than silently letting any request through.
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILogger<ApiKeyAuthAttribute>>();
                logger.LogError(
                    "ApiKeyAuthAttribute rejected request: server config slot {ConfigKey} is unset.",
                    ConfigKey);
                context.Result = new StatusCodeResult(StatusCodes.Status500InternalServerError);
                return Task.CompletedTask;
            }

            var providedBytes = System.Text.Encoding.UTF8.GetBytes(providedKey.ToString());
            var expectedBytes = System.Text.Encoding.UTF8.GetBytes(expectedKey);

            if (providedBytes.Length != expectedBytes.Length
                || !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes))
            {
                context.Result = new UnauthorizedResult();
                return Task.CompletedTask;
            }

            return Task.CompletedTask;
        }
    }
}
