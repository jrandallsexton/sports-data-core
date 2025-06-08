using SportsData.Core.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports
{
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.TeamInformation)]
    public class TeamInformationDocumentProcessor : IProcessDocuments
    {
        public Task ProcessAsync(ProcessDocumentCommand command)
        {
            throw new NotImplementedException();
        }
    }
}
