using SportsData.Core.Common;

namespace SportsData.Producer.Application.Documents.Processors;

public interface IDocumentProcessorFactory
{
    IProcessDocuments GetProcessor(SourceDataProvider sourceDataProvider, DocumentType documentType);
}

public class DocumentProcessorFactory : IDocumentProcessorFactory
{
    private readonly IServiceProvider _serviceProvider;

    public DocumentProcessorFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IProcessDocuments GetProcessor(SourceDataProvider sourceDataProvider, DocumentType documentType)
    {
        switch (sourceDataProvider)
        {
            case SourceDataProvider.Espn:
                return GetEspnDocumentProcessor(documentType);
            case SourceDataProvider.SportsDataIO:
            case SourceDataProvider.Cbs:
            case SourceDataProvider.Yahoo:
            default:
                throw new ArgumentOutOfRangeException(nameof(sourceDataProvider), sourceDataProvider, null);
        }
    }

    private IProcessDocuments GetEspnDocumentProcessor(DocumentType documentType)
    {
        switch (documentType)
        {
            case DocumentType.Athlete:
                return _serviceProvider.GetRequiredService<AthleteDocumentProcessor>();
            case DocumentType.Award:
                return _serviceProvider.GetRequiredService<AwardDocumentProcessor>();
            case DocumentType.Contest:
                return _serviceProvider.GetRequiredService<ContestDocumentProcessor>();
            case DocumentType.Franchise:
                return _serviceProvider.GetRequiredService<FranchiseDocumentProcessor>();
            case DocumentType.Team:
                return _serviceProvider.GetRequiredService<TeamDocumentProcessor>();
            case DocumentType.TeamBySeason:
                return _serviceProvider.GetRequiredService<TeamBySeasonDocumentProcessor>();
            case DocumentType.TeamInformation:
                return _serviceProvider.GetRequiredService<TeamInformationDocumentProcessor>();
            case DocumentType.Venue:
                return _serviceProvider.GetRequiredService<VenueDocumentProcessor>();
            case DocumentType.GameSummary:
            case DocumentType.Scoreboard:
            case DocumentType.Season:
            case DocumentType.Weeks:
            default:
                throw new ArgumentOutOfRangeException(nameof(documentType), documentType, null);
        }
    }
}
