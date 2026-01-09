using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;

using System;
using System.Collections.Generic;

namespace SportsData.Core.Eventing.Events.Documents
{
    public record DocumentCreated(
        string Id,
        string? ParentId,
        string Name,
        Uri? Ref,
        Uri SourceRef,
        string? DocumentJson,
        string SourceUrlHash,
        Sport Sport,
        int? SeasonYear,
        DocumentType DocumentType,
        SourceDataProvider SourceDataProvider,
        Guid CorrelationId,
        Guid CausationId,
        int AttemptCount = 0,
        IReadOnlyCollection<DocumentType>? IncludeLinkedDocumentTypes = null
    ) : EventBase(Ref, Sport, SeasonYear, CorrelationId, CausationId), IHasSourceUrlHashInitOnly;
}