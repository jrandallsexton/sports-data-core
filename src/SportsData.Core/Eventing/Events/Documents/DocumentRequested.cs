using System;

using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.Documents;

public record DocumentRequested(
    string Id,
    string? ParentId,
    Uri Uri,
    Sport Sport,
    int? SeasonYear,
    DocumentType DocumentType,
    SourceDataProvider SourceDataProvider,
    Guid CorrelationId,
    Guid CausationId,
    bool BypassCache = false
) : EventBase(CorrelationId, CausationId);