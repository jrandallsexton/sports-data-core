using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;

using System;

namespace SportsData.Core.Eventing.Events.Documents
{
    public record DocumentCreated(
        string Id,
        string? ParentId,
        string Name,
        string RoutingKey,
        string UrlHash,
        Sport Sport,
        int? SeasonYear,
        DocumentType DocumentType,
        SourceDataProvider SourceDataProvider,
        Guid CorrelationId,
        Guid CausationId
    ) : EventBase(CorrelationId, CausationId), IHasSourceUrlHashInitOnly;
}