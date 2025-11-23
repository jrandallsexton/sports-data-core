namespace SportsData.Core.Config;

/// <summary>
/// Configuration for OpenTelemetry instrumentation (tracing, metrics, logging)
/// </summary>
public class OpenTelemetryConfig
{
    public const string SectionName = "OpenTelemetry";

    /// <summary>
    /// Service name that will appear in traces/metrics (e.g., "SportsData.Api")
    /// </summary>
    public string ServiceName { get; set; } = "Unknown";

    /// <summary>
    /// Service version for tracking deployments
    /// </summary>
    public string ServiceVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Master kill switch - if false, no OTel instrumentation is added
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Distributed tracing configuration (Tempo)
    /// </summary>
    public TracingConfig Tracing { get; set; } = new();

    /// <summary>
    /// Metrics configuration (Prometheus)
    /// </summary>
    public MetricsConfig Metrics { get; set; } = new();

    /// <summary>
    /// Logging configuration (Loki via OTLP)
    /// </summary>
    public LoggingConfig Logging { get; set; } = new();
}

/// <summary>
/// Configuration for distributed tracing (sends to Tempo)
/// </summary>
public class TracingConfig
{
    /// <summary>
    /// Enable/disable tracing
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Sampling ratio (0.0 - 1.0)
    /// - 1.0 = 100% of requests traced (dev/staging)
    /// - 0.1 = 10% of requests traced (production)
    /// - 0.01 = 1% of requests traced (high-traffic prod)
    /// </summary>
    public double SamplingRatio { get; set; } = 1.0;

    /// <summary>
    /// OTLP gRPC endpoint for Tempo
    /// Dev: http://localhost:4317
    /// Prod: http://tempo.monitoring.svc.cluster.local:4317
    /// </summary>
    public string OtlpEndpoint { get; set; } = "http://localhost:4317";

    /// <summary>
    /// Timeout for OTLP exports (milliseconds)
    /// </summary>
    public int TimeoutMs { get; set; } = 10000;
}

/// <summary>
/// Configuration for metrics export (sends to Prometheus)
/// </summary>
public class MetricsConfig
{
    /// <summary>
    /// Enable/disable metrics
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// OTLP gRPC endpoint for Prometheus
    /// Dev: http://localhost:4317
    /// Prod: http://prometheus-server.monitoring.svc.cluster.local:4317
    /// </summary>
    public string OtlpEndpoint { get; set; } = "http://localhost:4317";

    /// <summary>
    /// HTTP endpoint for Prometheus scraping
    /// Exposed at this path (e.g., /metrics)
    /// </summary>
    public string PrometheusEndpoint { get; set; } = "/metrics";

    /// <summary>
    /// Timeout for OTLP exports (milliseconds)
    /// </summary>
    public int TimeoutMs { get; set; } = 10000;
}

/// <summary>
/// Configuration for structured logging via OTLP
/// Note: Loki uses HTTP port 3100, not OTLP. This requires an OTLP Collector.
/// </summary>
public class LoggingConfig
{
    /// <summary>
    /// Enable/disable OTLP logging
    /// </summary>
    public bool Enabled { get; set; } = false; // Disabled by default

    /// <summary>
    /// Loki HTTP endpoint (port 3100)
    /// Dev: http://localhost:3100
    /// Prod: http://loki.logging.svc.cluster.local:3100
    /// </summary>
    public string OtlpEndpoint { get; set; } = "http://localhost:3100";

    /// <summary>
    /// Timeout for OTLP exports (milliseconds)
    /// </summary>
    public int TimeoutMs { get; set; } = 10000;
}
