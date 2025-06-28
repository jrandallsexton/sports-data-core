using System;
using SportsData.Core.Common;

namespace SportsData.Core.Infrastructure.Clients.Provider.Commands;

public class GetExternalDocumentQuery(
    string? canonicalId,
    Uri uri,
    SourceDataProvider sourceDataProvider,
    Sport sport,
    DocumentType documentType,
    int? seasonYear)
{

    public string? CanonicalId { get; init; } = canonicalId;

    public Uri Uri { get; init; } = uri;

    public SourceDataProvider SourceDataProvider { get; init; } = sourceDataProvider;

    public Sport Sport { get; init; } = sport;

    public DocumentType DocumentType { get; init; } = documentType;

    public int? SeasonYear { get; init; } = seasonYear;
}