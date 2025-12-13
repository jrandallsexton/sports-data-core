using SportsData.Core.Common;

namespace SportsData.Provider.Application.ResourceIndex;

public record ProcessResourceIndexRequest
{
    /// <summary>
    /// Inclusion-only semantics: if this is provided and non-empty,
    /// downstream processors should only spawn linked documents that are in this list.
    /// If null or empty, all linked documents are processed (default behavior).
    /// </summary>
    public IReadOnlyCollection<DocumentType>? IncludeLinkedDocumentTypes { get; init; }
}