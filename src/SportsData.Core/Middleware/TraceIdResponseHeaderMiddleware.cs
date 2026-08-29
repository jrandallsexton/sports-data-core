using Microsoft.AspNetCore.Http;

using System.Diagnostics;
using System.Threading.Tasks;

namespace SportsData.Core.Middleware
{
    /// <summary>
    /// Stamps the request's W3C trace id onto every response as
    /// <c>X-Trace-Id</c>. The value is the SAME id OpenTelemetry propagates
    /// downstream via <c>traceparent</c> on outgoing HttpClient calls
    /// (e.g. API → Producer), and the same id Serilog writes to Seq — so
    /// one header value from the browser's network tab is a complete-trace
    /// query: <c>@TraceId = '&lt;value&gt;'</c> spans every service the
    /// request touched.
    ///
    /// Header is set via OnStarting so it survives whatever status code
    /// the pipeline ultimately produces (including 401/403/500).
    /// </summary>
    public class TraceIdResponseHeaderMiddleware
    {
        public const string HeaderName = "X-Trace-Id";

        private readonly RequestDelegate _next;

        public TraceIdResponseHeaderMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public Task InvokeAsync(HttpContext context)
        {
            context.Response.OnStarting(() =>
            {
                // Only the W3C Activity trace id — no TraceIdentifier
                // fallback. Serilog emits @TraceId to Seq only when an
                // Activity is active, so a fallback value would be a header
                // that matches nothing when pasted into Seq. Contract:
                // header present ⇒ the value IS queryable as @TraceId.
                // (With AspNetCore OTel instrumentation on, the Activity is
                // effectively always present; absence means tracing is off.)
                var traceId = Activity.Current?.TraceId.ToString();
                if (traceId is not null && !context.Response.Headers.ContainsKey(HeaderName))
                {
                    context.Response.Headers.Append(HeaderName, traceId);
                }

                return Task.CompletedTask;
            });

            return _next(context);
        }
    }
}
