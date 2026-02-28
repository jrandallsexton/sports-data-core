namespace SportsData.Core.Processing;

/// <summary>
/// Shared constants for document processing behavior across Producer components.
/// </summary>
public static class DocumentProcessingConstants
{
    /// <summary>
    /// Maximum number of processing attempts before a document is moved to the dead-letter queue.
    /// Used by DocumentCreatedHandler and DLQ reprocessing logic.
    /// </summary>
    public const int MaxAttempts = 10;
}
