using System;
using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.Documents;

public class DocumentRequested(
    string id,
    string href,
    Sport sport,
    int? seasonYear,
    DocumentType documentType,
    SourceDataProvider sourceDataProvider,
    Guid correlationId,
    Guid causationId)
    : EventBase(correlationId, causationId)
{
    public string Id { get; init; } = id;

    public string Href { get; init; } = href;

    public SourceDataProvider SourceDataProvider { get; init; } = sourceDataProvider;

    public Sport Sport { get; init; } = sport;

    public DocumentType DocumentType { get; init; } = documentType;

    public int? SeasonYear { get; init; } = seasonYear;
}