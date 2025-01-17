using SportsData.Core.Common;

namespace SportsData.Producer.Application.Documents.Processors.Football.Ncaa;

public interface IDocumentProcessorFactory
{
    IProcessDocuments GetProcessor(SourceDataProvider sourceDataProvider, Sport sport, DocumentType documentType);
}

public class DocumentProcessorFactory : IDocumentProcessorFactory
{
    private readonly IServiceProvider _serviceProvider;

    public DocumentProcessorFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IProcessDocuments GetProcessor(SourceDataProvider sourceDataProvider, Sport sport, DocumentType documentType)
    {
        switch (sourceDataProvider)
        {
            case SourceDataProvider.Espn:
                return GetEspnDocumentProcessor(sport, documentType);
            case SourceDataProvider.SportsDataIO:
            case SourceDataProvider.Cbs:
            case SourceDataProvider.Yahoo:
            default:
                throw new ArgumentOutOfRangeException(nameof(sourceDataProvider), sourceDataProvider, null);
        }
    }

    private IProcessDocuments GetEspnDocumentProcessor(Sport sport, DocumentType documentType)
    {
        switch (sport)
        {
            case Sport.FootballNcaa:
                return GetEspnFootballDocumentProcessor(documentType);
            case Sport.All:
            case Sport.Football:
            case Sport.FootballNfl:
            default:
                throw new ArgumentOutOfRangeException(nameof(sport), sport, null);
        }
    }

    private IProcessDocuments GetEspnFootballDocumentProcessor(DocumentType documentType)
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
            case DocumentType.GroupBySeason:
                return _serviceProvider.GetRequiredService<GroupBySeasonDocumentProcessor>();
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
            case DocumentType.CoachBySeason:
            default:
                throw new ArgumentOutOfRangeException(nameof(documentType), documentType, null);
        }
    }
}
