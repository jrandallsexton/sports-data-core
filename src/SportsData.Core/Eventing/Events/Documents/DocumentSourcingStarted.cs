using SportsData.Core.Common;

using System;

namespace SportsData.Core.Eventing.Events.Documents;

public record DocumentSourcingStarted(
    Sport Sport,
    int? SeasonYear,
    DocumentType DocumentType,
    int EstimatedDocumentCount,
    Guid CorrelationId,
    Guid CausationId
) : EventBase(CorrelationId, CausationId);