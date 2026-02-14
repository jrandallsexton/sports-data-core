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
}