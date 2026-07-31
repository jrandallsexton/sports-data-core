using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Common;
using SportsData.Core.Common.Mapping;
using SportsData.Core.Config;

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

            return prefixes is not null && prefixes.Any(p =>
                path.StartsWith(p, StringComparison.OrdinalIgnoreCase));
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

        if (!Allowlist.IsAllowed(service, opPath))
        {
            _logger.LogWarning(
                "Ops proxy refused non-allowlisted path. Service={Service}, Path={Path}",
                service, opPath);
            return NotFound();
        }

        var baseUrl = ResolveBaseUrl(service, mode);
        if (baseUrl is null)
        {
            _logger.LogError(
                "Ops proxy has no base URL configured. Service={Service}, Mode={Mode}",
                service, mode);
            return StatusCode(StatusCodes.Status502BadGateway,
                $"No internal base URL configured for {service}/{mode}.");
        }

        var targetUri = new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), opPath + Request.QueryString);

        using var upstreamRequest = new HttpRequestMessage(
            HttpMethod.Parse(Request.Method), targetUri);

        // Only the body crosses; inbound headers (bearer token, admin token,
        // cookies) deliberately do NOT — the internal services must never see
        // or depend on public-edge credentials.
        if (HttpMethods.IsPost(Request.Method))
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync(cancellationToken);
            upstreamRequest.Content = new StringContent(
                body, Encoding.UTF8, Request.ContentType ?? "application/json");
        }

        _logger.LogInformation(
            "Ops proxy relaying {Method} {Target}. Service={Service}, Mode={Mode}",
            Request.Method, targetUri, service, mode);

        var client = _httpClientFactory.CreateClient(nameof(AdminOpsProxyController));
        using var upstreamResponse = await client.SendAsync(upstreamRequest, cancellationToken);
        var responseBody = await upstreamResponse.Content.ReadAsStringAsync(cancellationToken);

        return new ContentResult
        {
            StatusCode = (int)upstreamResponse.StatusCode,
            Content = responseBody,
            ContentType = upstreamResponse.Content.Headers.ContentType?.ToString() ?? "application/json"
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
