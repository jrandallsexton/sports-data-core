using SportsData.Core.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football
{
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.Award)]
    public class AwardDocumentProcessor : IProcessDocuments
    {
        public Task ProcessAsync(ProcessDocumentCommand command)
        {
            throw new NotImplementedException();
        }
    }
}
