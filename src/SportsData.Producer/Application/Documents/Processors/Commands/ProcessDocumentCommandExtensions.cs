using SportsData.Core.Eventing.Events.Documents;

namespace SportsData.Producer.Application.Documents.Processors.Commands;

public static class ProcessDocumentCommandExtensions
{
    public static DocumentCreated ToDocumentCreated(this ProcessDocumentCommand command, int attemptCount = 0)
    {
        return new DocumentCreated(
            Id: command.UrlHash,
            ParentId: command.ParentId,
            Name: command.DocumentType.ToString(),
            Ref: command.OriginalUri ?? command.SourceUri,
            SourceRef: command.SourceUri,
            DocumentJson: command.Document,
            SourceUrlHash: command.UrlHash,
            Sport: command.Sport,
            SeasonYear: command.Season,
            DocumentType: command.DocumentType,
            SourceDataProvider: command.SourceDataProvider,
            CorrelationId: command.CorrelationId,
            CausationId: command.CorrelationId, // or use a new Guid if you want to track re-publishes distinctly
            AttemptCount: attemptCount
        );
    }
}