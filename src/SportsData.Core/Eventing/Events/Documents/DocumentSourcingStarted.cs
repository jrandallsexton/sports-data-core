using SportsData.Core.Common;

using System;

namespace SportsData.Core.Eventing.Events.Documents;

public record DocumentSourcingStarted(
    Uri? Ref,
    Sport Sport,
    int? SeasonYear,
    DocumentType DocumentType,
    int EstimatedDocumentCount,
    Guid CorrelationId,
    Guid CausationId
) : EventBase(Ref, Sport, SeasonYear, CorrelationId, CausationId);