using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Common;
using SportsData.Core.Common.Mapping;
using SportsData.Core.Config;
using SportsData.Core.Extensions;

using System.Net.Http.Headers;
using System.Text;

namespace SportsData.Api.Application.Admin;

/// <summary>
/// The single allowlisted pass-through for internal ops endpoints. Producer
/// and Provider came off public ingress on 2026-07-23 (unauthenticated ops
/// surface); this is the deliberate, bounded re-exposure: one route, admin
/// token required, an explicit path allowlist, GET/POST only.
///
///   POST /admin/ops/producer/football/nfl/api/franchise-seasons/seasonYear/2026/source
///
/// New op FAMILIES are one allowlist line; new endpoints inside an allowed
/// family need nothing at all. Sport/league resolve via ModeMapper (the house
/// convention), and the internal base URL comes from the same CommonConfig
/// keys the typed client factories use — per-sport Producer pods route
/// correctly in mode=All.
/// </summary>
[ApiController]
[Route("admin/ops")]
[AdminApiToken]
public class AdminOpsProxyController : ControllerBase
{
    /// <summary>
    /// What the proxy may reach, per service. Prefix match on the forwarded
    /// path. Deliberately explicit and code-level: the allowlist IS the
    /// security boundary that lets the rest of the internal API surface stay
    /// unreachable even with a valid admin token.
    /// </summary>
    public static class Allowlist
    {
        private static readonly string[] ProducerPrefixes =
        [
            "api/franchise-seasons",
            "api/competition",
            "api/contests"
        ];

        private static readonly string[] ProviderPrefixes =
        [
            "api/documents"
        ];

        public static bool IsAllowed(string service, string path)
        {
            var prefixes = service.ToLowerInvariant() switch
            {
                "producer" => ProducerPrefixes,
                "provider" => ProviderPrefixes,
                _ => null
            };

            // Segment boundary required: "api/franchise-seasons" must not
            // admit "api/franchise-seasons-admin-delete". Exact match or the
            // prefix followed by '/'.
            return prefixes is not null && prefixes.Any(p =>
                path.StartsWith(p, StringComparison.OrdinalIgnoreCase)
                && (path.Length == p.Length || path[p.Length] == '/'));
        }

        /// <summary>
        /// Resolves the target URI and checks the allowlist against the
        /// CANONICAL path that will actually be sent — not the raw opPath.
        /// URI construction normalizes dot-segments, so a raw check alone is
        /// bypassable: "api/contests/../../hangfire" starts with an allowed
        /// family but relays to /hangfire. Checking post-normalization closes
        /// literal traversal; rejecting any residual percent-escapes in the
        /// canonical path closes encoded and double-encoded variants (ops
        /// paths are plain segments — guids, years, slugs — so '%' has no
        /// legitimate use here).
        /// </summary>
        public static bool TryResolveAllowedTarget(
            string service,
            Uri baseUri,
            string opPath,
            string queryString,
            out Uri targetUri)
        {
            targetUri = new Uri(baseUri, opPath + queryString);

            if (!baseUri.IsBaseOf(targetUri))
                return false;

            var canonical = baseUri.MakeRelativeUri(targetUri).ToString();
            var queryIndex = canonical.IndexOf('?');
            if (queryIndex >= 0)
                canonical = canonical[..queryIndex];

            if (canonical.Contains('%'))
                return false;

            return IsAllowed(service, canonical);
        }
    }

    private readonly ILogger<AdminOpsProxyController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public AdminOpsProxyController(
        ILogger<AdminOpsProxyController> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet("{service}/{sport}/{league}/{**opPath}")]
    [HttpPost("{service}/{sport}/{league}/{**opPath}")]
    public async Task<IActionResult> Relay(
        [FromRoute] string service,
        [FromRoute] string sport,
        [FromRoute] string league,
        [FromRoute] string opPath,
        CancellationToken cancellationToken)
    {
        Sport mode;
        try
        {
            mode = ModeMapper.ResolveMode(sport, league);
        }
        catch (NotSupportedException)
        {
            return BadRequest($"Unsupported sport/league: {sport}/{league}");
        }

        var baseUrl = ResolveBaseUrl(service, mode);
        if (baseUrl is null)
        {
            _logger.LogError(
                "Ops proxy has no base URL configured. Service={Service}, Mode={Mode}",
                service.Sanitize(), mode);
            return StatusCode(StatusCodes.Status502BadGateway,
                $"No internal base URL configured for {service}/{mode}.");
        }

        // Resolve first, THEN allowlist-check the canonical result — the
        // check must see what will actually be sent, not the raw opPath
        // (dot-segment normalization would otherwise let a traversal path
        // relay outside its allowed family). See Allowlist.TryResolveAllowedTarget.
        Uri targetUri;
        try
        {
            // baseUri inside the try too: a malformed configured base is as
            // much a UriFormatException source as a malformed opPath, and
            // both should surface as 400, not an unhandled 500.
            var baseUri = new Uri(baseUrl.TrimEnd('/') + "/", UriKind.Absolute);
            if (!Allowlist.TryResolveAllowedTarget(
                    service, baseUri, opPath, Request.QueryString.Value ?? string.Empty, out targetUri))
            {
                _logger.LogWarning(
                    "Ops proxy refused path outside the allowlist after canonicalization. Service={Service}, Path={Path}",
                    service.Sanitize(), opPath.Sanitize());
                return NotFound();
            }
        }
        catch (UriFormatException)
        {
            return BadRequest("Invalid ops path.");
        }

        using var upstreamRequest = new HttpRequestMessage(
            HttpMethod.Parse(Request.Method), targetUri);

        // Only the body crosses; inbound headers (bearer token, admin token,
        // cookies) deliberately do NOT — the internal services must never see
        // or depend on public-edge credentials.
        if (HttpMethods.IsPost(Request.Method))
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync(cancellationToken);
            // NOT the 3-arg StringContent overload: it feeds the raw media
            // type to MediaTypeHeaderValue's bare type/subtype parser, so a
            // normal "application/json; charset=utf-8" would throw
            // FormatException before SendAsync. Parse-or-default instead.
            var content = new StringContent(body, Encoding.UTF8);
            content.Headers.ContentType =
                MediaTypeHeaderValue.TryParse(Request.ContentType, out var parsed)
                    ? parsed
                    : new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
            upstreamRequest.Content = content;
        }

        _logger.LogInformation(
            "Ops proxy relaying {Method} {Target}. Service={Service}, Mode={Mode}",
            Request.Method, targetUri.ToString().Sanitize(), service.Sanitize(), mode);

        var client = _httpClientFactory.CreateClient(nameof(AdminOpsProxyController));
        using var upstreamResponse = await client.SendAsync(upstreamRequest, cancellationToken);
        var responseBody = await upstreamResponse.Content.ReadAsStringAsync(cancellationToken);

        // The response content type is PINNED to JSON and marked nosniff
        // rather than echoing the upstream header: the ops surface speaks
        // JSON, and reflecting an upstream-declared type would let a
        // text/html body render in a browser — the classic reflected-XSS
        // sink CodeQL flags on proxies.
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return new ContentResult
        {
            StatusCode = (int)upstreamResponse.StatusCode,
            Content = responseBody,
            ContentType = "application/json"
        };
    }

    private string? ResolveBaseUrl(string service, Sport mode)
    {
        return service.ToLowerInvariant() switch
        {
            "producer" => _configuration[CommonConfigKeys.GetProducerProviderUri(mode)]
                          ?? _configuration[CommonConfigKeys.GetProducerProviderUri()],
            // Provider has no per-mode key helper today; probe the mode-keyed
            // shape first (matching Producer's convention and the sport-keyed
            // Prod.All config pattern) and fall back to the global key.
            "provider" => _configuration[$"{nameof(CommonConfig)}:ProviderClientConfig:{mode}:ApiUrl"]
                          ?? _configuration[CommonConfigKeys.GetProviderProviderUri()],
            _ => null
        };
    }
}
