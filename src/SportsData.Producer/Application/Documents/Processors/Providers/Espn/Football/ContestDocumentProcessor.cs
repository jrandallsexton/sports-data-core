using SportsData.Core.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football
{
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.Contest)]
    public class ContestDocumentProcessor : IProcessDocuments
    {
        public Task ProcessAsync(ProcessDocumentCommand command)
        {
            throw new NotImplementedException();
        }
    }
}
