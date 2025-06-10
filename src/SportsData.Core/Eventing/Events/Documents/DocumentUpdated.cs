using SportsData.Core.Common;

using System;

namespace SportsData.Core.Eventing.Events.Documents;

public record DocumentUpdated(
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
) : DocumentCreated(
    Id, ParentId, Name, RoutingKey, UrlHash, Sport, SeasonYear,
    DocumentType, SourceDataProvider, CorrelationId, CausationId
);