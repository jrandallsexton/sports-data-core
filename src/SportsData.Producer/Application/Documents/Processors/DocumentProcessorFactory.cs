using SportsData.Core.Common;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Common;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;
using SportsData.Producer.Config;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Documents.Processors;

public enum DocumentAction
{
    Created,
    Updated
}

public interface IDocumentProcessorFactory
{
    [Obsolete]
    IProcessDocuments GetProcessor(SourceDataProvider sourceDataProvider, Sport sport, DocumentType documentType, DocumentAction documentAction);
    IProcessDocuments GetProcessor(string routingKey, DocumentAction documentAction);
}

public class DocumentProcessorFactory : IDocumentProcessorFactory
{
    private readonly ILogger<DocumentProcessorFactory> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<(string RoutingKey, DocumentAction Action), string> _map;

    public DocumentProcessorFactory(
        IServiceProvider serviceProvider,
        DocumentProcessorMappings mappings,
        ILogger<DocumentProcessorFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        _map = mappings.Mappings.ToDictionary(
            m => (m.RoutingKey.ToLowerInvariant(), m.Action),
            m => m.ProcessorTypeName);
    }

    [Obsolete]
    public IProcessDocuments GetProcessor(SourceDataProvider sourceDataProvider, Sport sport, DocumentType documentType, DocumentAction documentAction)
    {
        switch (sourceDataProvider)
        {
            case SourceDataProvider.Espn:
                return GetEspnDocumentProcessor(sport, documentType, documentAction);
            case SourceDataProvider.SportsDataIO:
            case SourceDataProvider.Cbs:
            case SourceDataProvider.Yahoo:
            default:
                throw new ArgumentOutOfRangeException(nameof(sourceDataProvider), sourceDataProvider, null);
        }
    }

    public IProcessDocuments GetProcessor(string routingKey, DocumentAction documentAction)
    {
        throw new NotImplementedException();
    }

    [Obsolete]
    private IProcessDocuments GetEspnDocumentProcessor(Sport sport, DocumentType documentType, DocumentAction documentAction)
    {
        switch (sport)
        {
            case Sport.FootballNfl:
            case Sport.FootballNcaa:
                return GetEspnFootballDocumentProcessor(documentType, documentAction);
            case Sport.All:
            default:
                throw new ArgumentOutOfRangeException(nameof(sport), sport, null);
        }
    }

    [Obsolete]
    private IProcessDocuments GetEspnFootballDocumentProcessor(DocumentType documentType, DocumentAction documentAction)
    {
        switch (documentType)
        {
            case DocumentType.AthleteBySeason:
            case DocumentType.Athlete:
                return _serviceProvider.GetRequiredService<AthleteDocumentProcessor>();
            case DocumentType.Award:
                return _serviceProvider.GetRequiredService<AwardDocumentProcessor>();
            case DocumentType.Contest:
                return _serviceProvider.GetRequiredService<ContestDocumentProcessor>();
            case DocumentType.Franchise:
                return _serviceProvider.GetRequiredService<FranchiseDocumentProcessor<FootballDataContext>>();
            case DocumentType.GroupBySeason:
                return _serviceProvider.GetRequiredService<GroupBySeasonDocumentProcessor>();
            case DocumentType.Position:
                return _serviceProvider.GetRequiredService<PositionDocumentProcessor<FootballDataContext>>();
            //case DocumentType.Team:
            //    return _serviceProvider.GetRequiredService<TeamDocumentProcessor>();
            case DocumentType.TeamBySeason:
                return _serviceProvider.GetRequiredService<TeamSeasonDocumentProcessor<FootballDataContext>>();
            case DocumentType.TeamInformation:
                return _serviceProvider.GetRequiredService<TeamInformationDocumentProcessor>();
            case DocumentType.Venue:
                return _serviceProvider.GetRequiredService<VenueDocumentProcessor<FootballDataContext>>();
            case DocumentType.Seasons:
                return _serviceProvider.GetRequiredService<SeasonsDocumentProcessor>();
            case DocumentType.GameSummary:
            case DocumentType.Scoreboard:
            case DocumentType.Season:
            case DocumentType.Weeks:
            case DocumentType.CoachBySeason:
            case DocumentType.GroupLogo:
            case DocumentType.FranchiseLogo:
            case DocumentType.GroupBySeasonLogo:
            case DocumentType.TeamBySeasonLogo:
            case DocumentType.VenueImage:
            default:
                throw new ArgumentOutOfRangeException(nameof(documentType), documentType, null);
        }
    }
}
