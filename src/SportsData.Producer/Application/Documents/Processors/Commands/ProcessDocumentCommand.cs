﻿using SportsData.Core.Common;

namespace SportsData.Producer.Application.Documents.Processors.Commands;

public class ProcessDocumentCommand(
    SourceDataProvider sourceDataProvider,
    Sport sport,
    int? season,
    DocumentType documentType,
    string document,
    Guid correlationId,
    string? parentId,
    Uri sourceUri,
    string urlHash)
{
    public SourceDataProvider SourceDataProvider { get; init; } = sourceDataProvider;

    public Sport Sport { get; init; } = sport;

    public DocumentType DocumentType { get; init; } = documentType;

    public string Document { get; init; } = document;

    public Guid CorrelationId { get; init; } = correlationId;

    public int? Season { get; init; } = season;

    public string? ParentId { get; set; } = parentId;

    public Uri SourceUri { get; init; } = sourceUri;

    public string UrlHash { get; init; } = urlHash;
}