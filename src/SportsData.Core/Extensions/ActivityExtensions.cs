using System;
using System.Diagnostics;

namespace SportsData.Core.Extensions
{
    /// <summary>
    /// Extensions for working with OpenTelemetry Activity (distributed tracing).
    /// </summary>
    public static class ActivityExtensions
    {
        /// <summary>
        /// Gets the current correlation ID from the OpenTelemetry TraceId.
        /// If no Activity is present, generates a new correlation ID.
        /// </summary>
        /// <returns>Correlation ID as Guid</returns>
        public static Guid GetCorrelationId()
        {
            var activity = Activity.Current;
            
            if (activity != null && activity.TraceId != default)
            {
                // Convert TraceId string (32 hex chars) to Guid
                // TraceId format: "0af7651916cd43dd8448eb211c80319c" (no hyphens)
                // Guid format:    "0af76519-16cd-43dd-8448-eb211c80319c" (with hyphens)
                var traceIdString = activity.TraceId.ToString();
                
                if (traceIdString.Length == 32)
                {
                    // Insert hyphens to match Guid format: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
                    var guidString = string.Format(
                        "{0}-{1}-{2}-{3}-{4}",
                        traceIdString.Substring(0, 8),   // xxxxxxxx
                        traceIdString.Substring(8, 4),   // xxxx
                        traceIdString.Substring(12, 4),  // xxxx
                        traceIdString.Substring(16, 4),  // xxxx
                        traceIdString.Substring(20, 12)  // xxxxxxxxxxxx
                    );
                    
                    return Guid.Parse(guidString);
                }
            }

            // Fallback: generate new correlation ID if no trace context exists
            return Guid.NewGuid();
        }

        /// <summary>
        /// Gets the current causation ID from the OpenTelemetry SpanId.
        /// This represents the immediate parent operation that caused this operation.
        /// </summary>
        /// <returns>Causation ID as string (SpanId is 8 bytes)</returns>
        public static string GetCausationId()
        {
            var activity = Activity.Current;
            return activity?.SpanId.ToString() ?? Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// Gets both correlation and causation IDs for logging.
        /// </summary>
        public static (Guid CorrelationId, string CausationId) GetTraceIds()
        {
            return (GetCorrelationId(), GetCausationId());
        }
    }
}
