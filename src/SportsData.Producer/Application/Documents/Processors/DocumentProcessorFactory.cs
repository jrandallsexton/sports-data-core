﻿using SportsData.Core.Common;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Shared;

namespace SportsData.Producer.Application.Documents.Processors;

public enum DocumentAction
{
    Created,
    Updated
}

public interface IDocumentProcessorFactory
{
    IProcessDocuments GetProcessor(SourceDataProvider sourceDataProvider, Sport sport, DocumentType documentType, DocumentAction documentAction);
}

public class DocumentProcessorFactory : IDocumentProcessorFactory
{
    private readonly IServiceProvider _serviceProvider;

    public DocumentProcessorFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

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
                return _serviceProvider.GetRequiredService<FranchiseDocumentProcessor>();
            case DocumentType.GroupBySeason:
                return _serviceProvider.GetRequiredService<GroupBySeasonDocumentProcessor>();
            case DocumentType.Position:
                return _serviceProvider.GetRequiredService<PositionDocumentProcessor>();
            case DocumentType.Team:
                return _serviceProvider.GetRequiredService<TeamDocumentProcessor>();
            case DocumentType.TeamBySeason:
                return _serviceProvider.GetRequiredService<TeamSeasonDocumentProcessor>();
            case DocumentType.TeamInformation:
                return _serviceProvider.GetRequiredService<TeamInformationDocumentProcessor>();
            case DocumentType.Venue:
                return documentAction == DocumentAction.Created
                    ? _serviceProvider.GetRequiredService<VenueCreatedDocumentProcessor>()
                    : _serviceProvider.GetRequiredService<VenueUpdatedDocumentProcessor>();
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
