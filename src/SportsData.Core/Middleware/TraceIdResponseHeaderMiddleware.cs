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
                if (!context.Response.Headers.ContainsKey(HeaderName))
                {
                    // Activity is created by the AspNetCore OTel instrumentation;
                    // TraceIdentifier is the framework fallback when tracing is off.
                    var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
                    context.Response.Headers.Append(HeaderName, traceId);
                }

                return Task.CompletedTask;
            });

            return _next(context);
        }
    }
}
