using SportsData.Core.Common;

namespace SportsData.Producer.Application.Documents.Processors;

public class ProcessDocumentCommand(
    SourceDataProvider sourceDataProvider,
    Sport sport,
    DocumentType documentType,
    string document,
    Guid correlationId)
{
    public SourceDataProvider SourceDataProvider { get; } = sourceDataProvider;

    public Sport Sport { get; } = sport;

    public DocumentType DocumentType { get; } = documentType;

    public string Document { get; } = document;

    public Guid CorrelationId { get; } = correlationId;
}