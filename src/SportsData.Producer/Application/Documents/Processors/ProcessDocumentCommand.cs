using SportsData.Core.Common;

namespace SportsData.Producer.Application.Documents.Processors;

public class ProcessDocumentCommand(
    SourceDataProvider sourceDataProvider,
    string document,
    Guid correlationId)
{
    public SourceDataProvider SourceDataProvider { get; } = sourceDataProvider;

    public string Document { get; } = document;

    public Guid CorrelationId { get; } = correlationId;
}