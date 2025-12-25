using Microsoft.Extensions.Logging;

using Polly;

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace SportsData.Core.Http.Policies
{
    public static class RetryPolicy
    {
        /// <summary>
        /// Reusable HTTP retry policy with exponential backoff + jitter and structured logging.
        /// </summary>
        /// <remarks>
        /// Retries on:
        /// - Network errors (HttpRequestException, IOException, TaskCanceledException)
        /// - 5xx server errors (500, 502, 503, 504 - transient server/infrastructure failures)
        /// - 408 Request Timeout (network timeout)
        /// - 429 Too Many Requests (rate limiting with exponential backoff)
        /// - 403 Forbidden from ESPN API (non-standard use for IP-based rate limiting)
        /// 
        /// Does NOT retry on:
        /// - 401 Unauthorized (missing/invalid credentials)
        /// - 403 Forbidden from non-ESPN APIs (insufficient permissions)
        /// - 4xx client errors (bad request, not found, etc.)
        /// 
        /// Real-world observations:
        /// - ESPN API (public, no auth) occasionally returns 503 Service Unavailable during maintenance/load spikes
        /// - ESPN API returns 403 Forbidden for IP-based rate limiting (non-standard, should use 429)
        /// - 503 errors are transient and resolve within seconds to minutes
        /// - 403 errors from ESPN require longer backoff to allow rate limit window to reset
        /// - Exponential backoff with jitter prevents thundering herd on recovery
        /// </remarks>
        public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(ILogger? logger = null)
        {
            // TODO: Extract the retry count and delay from a config file
            const int retryCount = 3;
            var baseDelay = TimeSpan.FromMilliseconds(200);
            var rng = new Random();

            return Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .Or<IOException>()
                .Or<TaskCanceledException>() // includes timeouts
                .OrResult(r =>
                {
                    // 🚨 SPECIAL CASE: ESPN API uses 403 for IP-based rate limiting (non-standard)
                    if (r.StatusCode == HttpStatusCode.Forbidden)
                    {
                        var requestUri = r.RequestMessage?.RequestUri?.AbsoluteUri ?? "";
                        
                        // ESPN uses 403 for rate limiting instead of standard 429
                        // Detected via: VPN switch immediately resolved 403, indicating IP-based blocking
                        if (requestUri.Contains("espn.com", StringComparison.OrdinalIgnoreCase))
                        {
                            logger?.LogWarning(
                                "HTTP 403 from ESPN API - Likely IP-based rate limiting (non-standard). " +
                                "Uri={RequestUri} Reason={ReasonPhrase}. Will retry with extended backoff.",
                                r.RequestMessage?.RequestUri,
                                r.ReasonPhrase);
                            
                            return true; // ✅ RETRY for ESPN rate limiting
                        }
                        
                        // All other APIs: 403 is an authorization error, fail fast
                        logger?.LogError(
                            "HTTP 403 Forbidden - Authorization/permission denied. Check API credentials and permissions. " +
                            "Uri={RequestUri} Reason={ReasonPhrase}",
                            r.RequestMessage?.RequestUri,
                            r.ReasonPhrase);
                        
                        return false; // ❌ DO NOT RETRY for non-ESPN auth errors
                    }

                    // ✅ RETRY: Transient server/infrastructure errors and rate limiting
                    // 500-599: Server errors (includes 503 Service Unavailable)
                    // 408: Request Timeout
                    // 429: Too Many Requests (rate limiting)
                    return (int)r.StatusCode >= 500 ||
                           r.StatusCode == HttpStatusCode.RequestTimeout ||
                           r.StatusCode == HttpStatusCode.TooManyRequests;
                })
                .WaitAndRetryAsync(
                    retryCount,
                    attempt =>
                    {
                        var exp = Math.Pow(2, attempt - 1); // 1,2,4
                        var jitterMs = rng.Next(25, 125);
                        return TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * exp + jitterMs);
                    },
                    (outcome, delay, attempt, context) =>
                    {
                        var status = outcome.Result?.StatusCode;
                        var ex = outcome.Exception;
                        
                        // Enhanced logging for 503 to track transient outages
                        if (status == HttpStatusCode.ServiceUnavailable)
                        {
                            logger?.LogWarning(
                                "HTTP 503 Service Unavailable (transient outage) - Retry {Attempt}/{MaxRetries} in {Delay}ms. " +
                                "Uri={RequestUri} Reason={ReasonPhrase}",
                                attempt,
                                retryCount,
                                delay.TotalMilliseconds,
                                outcome.Result?.RequestMessage?.RequestUri,
                                outcome.Result?.ReasonPhrase);
                        }
                        // Enhanced logging for ESPN 403 rate limiting
                        else if (status == HttpStatusCode.Forbidden)
                        {
                            logger?.LogWarning(
                                "HTTP 403 from ESPN (IP rate limiting) - Retry {Attempt}/{MaxRetries} in {Delay}ms. " +
                                "Uri={RequestUri} Reason={ReasonPhrase}. Consider increasing RequestDelayMs if this persists.",
                                attempt,
                                retryCount,
                                delay.TotalMilliseconds,
                                outcome.Result?.RequestMessage?.RequestUri,
                                outcome.Result?.ReasonPhrase);
                        }
                        else
                        {
                            logger?.LogWarning(
                                ex,
                                "HTTP retry {Attempt}/{MaxRetries} in {Delay}ms. Status={Status} Reason={ReasonPhrase} " +
                                "Uri={RequestUri} PolicyKey={PolicyKey} OperationKey={OperationKey}",
                                attempt,
                                retryCount,
                                delay.TotalMilliseconds,
                                status.HasValue ? (int)status.Value : 0,
                                outcome.Result?.ReasonPhrase,
                                outcome.Result?.RequestMessage?.RequestUri,
                                context.PolicyKey,
                                context.OperationKey);
                        }
                    });
        }
    }
}
