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

    /// <summary>
    /// Value stamped on the <c>RetryReason</c> header of messages re-published from the DLQ.
    /// Read by <c>DocumentCreatedHandler</c> to bypass the exponential backoff schedule and
    /// schedule the message for immediate processing.
    /// </summary>
    public const string DlqReprocessRetryReason = "DlqReprocess";
}
