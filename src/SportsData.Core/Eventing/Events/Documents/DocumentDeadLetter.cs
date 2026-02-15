using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;

using System;

namespace SportsData.Core.Eventing.Events.Documents
{
    /// <summary>
    /// Published when a document fails processing after maximum retry attempts.
    /// Used for monitoring, alerting, and observability.
    /// </summary>
    public record DocumentDeadLetter(
        string Id,
        string? ParentId,
        Uri? Ref,
        Uri SourceRef,
        string SourceUrlHash,
        Sport Sport,
        int? SeasonYear,
        DocumentType DocumentType,
        SourceDataProvider SourceDataProvider,
        int AttemptCount,
        string Reason,
        Guid CorrelationId,
        Guid CausationId
    ) : EventBase(Ref, Sport, SeasonYear, CorrelationId, CausationId), IHasSourceUrlHashInitOnly;
}
