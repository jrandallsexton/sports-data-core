using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Documents;
using System.Text.Json;

namespace SportsData.Producer.Application.Documents.Processors.Commands;

public class ProcessDocumentCommand(
    SourceDataProvider sourceDataProvider,
    Sport sport,
    int? seasonYear,
    DocumentType documentType,
    string document,
    Guid messageId,
    Guid correlationId,
    string? parentId,
    Uri sourceUri,
    string urlHash,
    Uri? originalUri = null,
    int attemptCount = 0,
    IReadOnlyCollection<DocumentType>? includeLinkedDocumentTypes = null,
    bool notifyOnCompletion = false,
    bool priority = false)
{
    public SourceDataProvider SourceDataProvider { get; init; } = sourceDataProvider;

    public Sport Sport { get; init; } = sport;

    public DocumentType DocumentType { get; init; } = documentType;

    public string Document { get; init; } = document;

    public Guid MessageId { get; init; } = messageId;

    public Guid CorrelationId { get; init; } = correlationId;

    public int? SeasonYear { get; init; } = seasonYear;

    public string? ParentId { get; set; } = parentId;

    public Uri SourceUri { get; init; } = sourceUri;

    public string UrlHash { get; init; } = urlHash;

    public Uri? OriginalUri { get; init; } = originalUri;

    public int AttemptCount { get; init; } = attemptCount;

    /// <summary>
    /// Optional inclusion-only list of linked document types. Three meanings:
    /// null = no filter, spawn all linked documents (default); EMPTY = spawn none —
    /// this document is wanted for itself alone (FK-only dependency requests);
    /// non-empty = spawn only the listed types. The filter propagates onto every
    /// request this command publishes, so an empty filter keeps the entire chain
    /// beneath it lean. See docs/features/athlete-cascade-scoping.md.
    /// </summary>
    public IReadOnlyCollection<DocumentType>? IncludeLinkedDocumentTypes { get; init; } = includeLinkedDocumentTypes;

    /// <summary>
    /// When true, Producer will publish DocumentProcessingCompleted event after successfully processing this document.
    /// Used by Provider saga to orchestrate tier progression in historical sourcing.
    /// </summary>
    public bool NotifyOnCompletion { get; init; } = notifyOnCompletion;

    /// <summary>
    /// True when this document rides the "live" Hangfire queue — streamer-
    /// originated work for a contest backing a pick'em league (#688 scoping
    /// makes streamer-originated == league-live). Propagates onto every
    /// request this command publishes so a live play's FK dependencies do
    /// not stall behind bulk backfill. See athlete-cascade-scoping.md item 5.
    /// </summary>
    public bool Priority { get; init; } = priority;

    public Dictionary<string, string> PropertyBag = new Dictionary<string, string>();

    /// <summary>
    /// Tracks which dependency documents have already been requested to prevent duplicate requests on retries.
    /// Key is (DocumentType, UrlHash) to uniquely identify each dependency.
    /// Example: A competition may depend on two different Franchises - tracking by DocumentType alone would skip the second.
    /// </summary>
    public HashSet<RequestedDependency> RequestedDependencies { get; set; } = new();

    /// <summary>
    /// Extracts the ESPN $ref URI from the JSON document for logging purposes.
    /// Returns null if $ref cannot be found or parsed.
    /// </summary>
    /// <returns>The $ref URI as a string, or null if not found</returns>
    public string? GetDocumentRef()
    {
        try
        {
            using var jsonDoc = JsonDocument.Parse(Document);
            if (jsonDoc.RootElement.TryGetProperty("$ref", out var refElement))
            {
                return refElement.GetString();
            }
        }
        catch
        {
            // Silently ignore parsing errors - this is best-effort logging
        }

        return null;
    }

    /// <summary>
    /// Gets a safe subset of command properties for logging (excludes large Document JSON).
    /// </summary>
    /// <returns>Anonymous object with safe logging properties</returns>
    /// <summary>
    /// Gets a dictionary of command properties for use in logging scopes.
    /// Provides standardized contextual logging across all document processors.
    /// </summary>
    /// <returns>Dictionary with alphabetically sorted scope properties</returns>
    public Dictionary<string, object> ToLogScope()
    {
        return new Dictionary<string, object>
        {
            ["AttemptCount"] = AttemptCount,
            ["CorrelationId"] = CorrelationId,
            ["DocumentType"] = DocumentType,
            ["MessageId"] = MessageId,
            ["NotifyOnCompletion"] = NotifyOnCompletion,
            ["Priority"] = Priority,
            ["ParentId"] = ParentId ?? string.Empty,
            ["Ref"] = GetDocumentRef() ?? string.Empty,
            ["SeasonYear"] = SeasonYear ?? -1,
            ["SourceDataProvider"] = SourceDataProvider,
            ["SourceUri"] = SourceUri.ToString(),
            ["Sport"] = Sport,
            ["UrlHash"] = UrlHash
        };
    }
}