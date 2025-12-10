namespace SportsData.Producer.Config;

/// <summary>
/// Configuration options for document processing behavior
/// </summary>
public class DocumentProcessingConfig
{
    /// <summary>
    /// When true, document processors will reactively request missing dependencies via DocumentRequested events (legacy/override mode).
    /// When false (default/recommended), processors throw ExternalDocumentNotSourcedException and rely on Hangfire retries.
    /// 
    /// Setting this to false enforces proper source ordering and prevents circular dependency issues.
    /// Can be overridden to true for specific edge cases where reactive requests are needed.
    /// 
    /// Default: false (safe mode - no reactive dependency requests)
    /// </summary>
    public bool EnableDependencyRequests { get; set; } = false;
}
