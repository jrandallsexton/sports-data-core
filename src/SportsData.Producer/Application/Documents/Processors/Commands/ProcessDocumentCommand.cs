using SportsData.Core.Common;

namespace SportsData.Producer.Application.Documents.Processors.Commands;

public class ProcessDocumentCommand(
    SourceDataProvider sourceDataProvider,
    Sport sport,
    int? season,
    DocumentType documentType,
    string document,
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
}