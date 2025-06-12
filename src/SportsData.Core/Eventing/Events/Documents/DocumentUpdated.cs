using SportsData.Core.Common;

using System;

namespace SportsData.Core.Eventing.Events.Documents;

public record DocumentUpdated(
    string Id,
    string? ParentId,
    string Name,
    Uri Ref,
    string UrlHash,
    Sport Sport,
    int? SeasonYear,
    DocumentType DocumentType,
    SourceDataProvider SourceDataProvider,
    Guid CorrelationId,
    Guid CausationId
) : DocumentCreated(
    Id, ParentId, Name, Ref, UrlHash, Sport, SeasonYear,
    DocumentType, SourceDataProvider, CorrelationId, CausationId
);