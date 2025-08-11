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
                    (int)r.StatusCode >= 500 ||
                    r.StatusCode == HttpStatusCode.RequestTimeout)
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
                        logger?.LogWarning(
                            ex,
                            "HTTP retry {Attempt} in {Delay}. Status={Status} Reason={ReasonPhrase} PolicyKey={PolicyKey} OperationKey={OperationKey}",
                            attempt,
                            delay,
                            status.HasValue ? (int)status.Value : 0,
                            outcome.Result?.ReasonPhrase,
                            context.PolicyKey,
                            context.OperationKey);
                    });
        }
    }
}
