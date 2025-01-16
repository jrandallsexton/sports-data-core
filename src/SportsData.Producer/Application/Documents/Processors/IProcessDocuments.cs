namespace SportsData.Producer.Application.Documents.Processors;

public interface IProcessDocuments
{
    Task ProcessAsync(ProcessDocumentCommand command);
}