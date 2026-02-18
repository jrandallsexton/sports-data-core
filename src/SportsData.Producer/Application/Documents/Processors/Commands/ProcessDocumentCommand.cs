using SportsData.Core.Common;
using System.Text.Json;

namespace SportsData.Producer.Application.Documents.Processors.Commands;

public class ProcessDocumentCommand(
    SourceDataProvider sourceDataProvider,
    Sport sport,
    int? season,
    DocumentType documentType,
    string document,
    Guid messageId,
    Guid correlationId,
    string? parentId,
    Uri sourceUri,
    string urlHash,
    Uri? originalUri = null,
    int attemptCount = 0,
    IReadOnlyCollection<DocumentType>? includeLinkedDocumentTypes = null)
{
    public SourceDataProvider SourceDataProvider { get; init; } = sourceDataProvider;

    public Sport Sport { get; init; } = sport;

    public DocumentType DocumentType { get; init; } = documentType;

    public string Document { get; init; } = document;

    public Guid MessageId { get; init; } = messageId;

    public Guid CorrelationId { get; init; } = correlationId;

    public int? Season { get; init; } = season;

    public string? ParentId { get; set; } = parentId;

    public Uri SourceUri { get; init; } = sourceUri;

    public string UrlHash { get; init; } = urlHash;

    public Uri? OriginalUri { get; init; } = originalUri;

    public int AttemptCount { get; init; } = attemptCount;

    /// <summary>
    /// Optional inclusion-only list of linked document types.
    /// If provided and non-empty, downstream processors should only spawn linked documents
    /// of types in this collection. If null or empty, all linked documents are processed.
    /// </summary>
    public IReadOnlyCollection<DocumentType>? IncludeLinkedDocumentTypes { get; init; } = includeLinkedDocumentTypes;

    public Dictionary<string, string> PropertyBag = new Dictionary<string, string>();

    /// <summary>
    /// Tracks which dependency documents have already been requested to prevent duplicate requests on retries.
    /// Key is (DocumentType, UrlHash) to uniquely identify each dependency.
    /// Example: A competition may depend on two different Franchises - tracking by DocumentType alone would skip the second.
    /// </summary>
    public HashSet<(DocumentType Type, string UrlHash)> RequestedDependencies { get; set; } = new();

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
    public object ToSafeLogObject()
    {
        return new
        {
            DocumentType,
            Sport,
            Season,
            ParentId,
            UrlHash,
            AttemptCount,
            Ref = GetDocumentRef(), // ✅ ESPN $ref URI for Postman debugging
            SourceUri = SourceUri.ToString()
        };
    }

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
            // TODO: ["CausationId"] = CausationId, 
            ["CorrelationId"] = CorrelationId,
            ["DocumentType"] = DocumentType,
            ["MessageId"] = MessageId,
            ["ParentId"] = ParentId ?? string.Empty,
            ["Ref"] = GetDocumentRef() ?? string.Empty,
            ["Season"] = Season ?? 0,
            ["SourceDataProvider"] = SourceDataProvider,
            ["SourceUri"] = SourceUri.ToString(),
            ["Sport"] = Sport,
            ["UrlHash"] = UrlHash
        };
    }
}