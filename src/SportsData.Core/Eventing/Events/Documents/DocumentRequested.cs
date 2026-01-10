using System;
using System.Collections.Generic;
using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.Documents;

public record DocumentRequested(
    string Id,
    string? ParentId,
    Uri Uri,
    Uri? Ref,
    Sport Sport,
    int? SeasonYear,
    DocumentType DocumentType,
    SourceDataProvider SourceDataProvider,
    Guid CorrelationId,
    Guid CausationId,
    Dictionary<string, string>? PropertyBag = null
) : EventBase(Ref, Sport, SeasonYear, CorrelationId, CausationId);