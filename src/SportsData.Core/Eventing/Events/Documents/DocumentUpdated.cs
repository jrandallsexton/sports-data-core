using SportsData.Core.Common;

using System;

namespace SportsData.Core.Eventing.Events.Documents;

public record DocumentUpdated(
    string Id,
    string? ParentId,
    string Name,
    Uri Ref,
    Uri SourceRef,
    string? DocumentJson,
    string UrlHash,
    Sport Sport,
    int? SeasonYear,
    DocumentType DocumentType,
    SourceDataProvider SourceDataProvider,
    Guid CorrelationId,
    Guid CausationId
) : DocumentCreated(
    Id, ParentId, Name, Ref, SourceRef, DocumentJson, UrlHash, Sport, SeasonYear,
    DocumentType, SourceDataProvider, CorrelationId, CausationId
);