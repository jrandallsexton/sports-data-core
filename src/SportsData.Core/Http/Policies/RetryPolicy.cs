using Polly;

using System;
using System.Net;
using System.Net.Http;

namespace SportsData.Core.Http.Policies
{
    public static class RetryPolicy
    {
        public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
        {
            // TODO: Extract the retry count and delay from a config file
            return Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .OrResult(response =>
                    (int)response.StatusCode >= 500 ||
                    response.StatusCode == HttpStatusCode.RequestTimeout)
                .WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (outcome, timespan, retryAttempt, context) =>
                    {
                        Console.WriteLine(
                            $"Polly retry {retryAttempt} after {timespan}. Status: {outcome?.Result?.StatusCode}");
                    });
        }
    }
}