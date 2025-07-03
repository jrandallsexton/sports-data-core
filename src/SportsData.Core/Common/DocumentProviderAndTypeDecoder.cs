using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;

using System;

namespace SportsData.Core.Common
{
    public interface IDecodeDocumentProvidersAndTypes
    {
        Type GetType(SourceDataProvider sourceDataProvider, DocumentType docType);

        (Type Type, string CollectionName) GetTypeAndCollectionName(SourceDataProvider sourceDataProvider, Sport sport, DocumentType docType, int? season);

        string GetCollectionName(SourceDataProvider sourceDataProvider, Sport sport, DocumentType docType, int? season);

        DocumentType GetLogoDocumentTypeFromDocumentType(DocumentType documentType);
    }

    public class DocumentProviderAndTypeDecoder : IDecodeDocumentProvidersAndTypes
    {
        public Type GetType(SourceDataProvider sourceDataProvider, DocumentType docType)
        {
            switch (docType)
            {
                case DocumentType.Franchise:
                    return typeof(EspnFranchiseDto);
                case DocumentType.TeamSeason:
                    return typeof(EspnTeamSeasonDto);
                case DocumentType.Venue:
                    return typeof(EspnVenueDto);
                case DocumentType.CoachSeason:
                    return typeof(EspnCoachSeasonDto);
                case DocumentType.Athlete:
                    return typeof(EspnAthleteDto);
                case DocumentType.GroupBySeason:
                    return typeof(EspnGroupBySeasonDto);
                case DocumentType.Position:
                    return typeof(EspnAthletePositionDto);
                case DocumentType.AthleteBySeason:
                    return typeof(EspnAthleteDto);
                case DocumentType.Award:
                case DocumentType.Contest:
                // TODO: Create these => return typeof(EspnContestDto);
                case DocumentType.GameSummary:
                case DocumentType.Scoreboard:
                case DocumentType.Season:
                case DocumentType.TeamInformation:
                case DocumentType.Weeks:
                case DocumentType.GroupLogo:
                case DocumentType.FranchiseLogo:
                case DocumentType.GroupBySeasonLogo:
                case DocumentType.TeamBySeasonLogo:
                case DocumentType.VenueImage:
                case DocumentType.AthleteImage:
                default:
                    throw new ArgumentOutOfRangeException(nameof(docType), docType, null);
            }
        }

        public (Type, string) GetTypeAndCollectionName(SourceDataProvider sourceDataProvider, Sport sport, DocumentType docType, int? season)
        {
            var name = GetCollectionName(sourceDataProvider, sport, docType, season);

            switch (docType)
            {
                case DocumentType.Franchise:
                    return (typeof(EspnFranchiseDto), name);
                case DocumentType.TeamSeason:
                    return (typeof(EspnTeamSeasonDto), name);
                case DocumentType.Venue:
                    return (typeof(EspnVenueDto), name);
                case DocumentType.CoachSeason:
                    return (typeof(EspnCoachSeasonDto), name);
                case DocumentType.Athlete:
                    return (typeof(EspnAthleteDto), name);
                case DocumentType.GroupBySeason:
                    return (typeof(EspnGroupBySeasonDto), name);
                case DocumentType.Position:
                    return (typeof(EspnAthletePositionDto), name);
                case DocumentType.AthleteBySeason:
                    return (typeof(EspnAthleteDto), name);
                case DocumentType.Seasons:
                    return (typeof(EspnFootballSeasonsDto), name);
                case DocumentType.SeasonType:
                    return (typeof(EspnResourceIndexDto), name);
                case DocumentType.Season:
                case DocumentType.Award:
                    return (typeof(EspnAwardDto), name);
                case DocumentType.Group:
                    // TODO: This is a placeholder; should be EspnGroupDto when implemented
                    return (typeof(EspnGroupBySeasonDto), name); 
                case DocumentType.Contest:
                    return (typeof(EspnFootballContestDto), name);
                case DocumentType.GameSummary:
                case DocumentType.Scoreboard:
                case DocumentType.TeamInformation:
                case DocumentType.Weeks:
                case DocumentType.GroupLogo:
                case DocumentType.FranchiseLogo:
                case DocumentType.GroupBySeasonLogo:
                case DocumentType.TeamBySeasonLogo:
                case DocumentType.VenueImage:
                case DocumentType.AthleteImage:
                case DocumentType.GolfCalendar:
                case DocumentType.Standings:
                case DocumentType.TeamRank:
                default:
                    throw new ArgumentOutOfRangeException(nameof(docType), docType, null);
            }
        }

        public string GetCollectionName(SourceDataProvider sourceDataProvider, Sport sport, DocumentType docType, int? season)
        {
            return docType.ToString();
            //return season.HasValue ?
            //    $"{sourceDataProvider.ToString()}{sport.ToString()}{docType.ToString()}{season.Value}" :
            //    $"{sourceDataProvider.ToString()}{sport.ToString()}{docType.ToString()}";
        }

        public DocumentType GetLogoDocumentTypeFromDocumentType(DocumentType documentType)
        {
            switch (documentType)
            {
                case DocumentType.Athlete:
                case DocumentType.AthleteBySeason:
                    return DocumentType.AthleteImage;
                case DocumentType.Franchise:
                case DocumentType.FranchiseLogo:
                    return DocumentType.FranchiseLogo;
                case DocumentType.GroupLogo:
                case DocumentType.GroupBySeason:
                case DocumentType.GroupBySeasonLogo:
                    return DocumentType.GroupBySeasonLogo;
                case DocumentType.TeamSeason:
                case DocumentType.TeamBySeasonLogo:
                    return DocumentType.TeamBySeasonLogo;
                case DocumentType.Venue:
                case DocumentType.VenueImage:
                    return DocumentType.VenueImage;
                case DocumentType.Award:
                case DocumentType.CoachSeason:
                case DocumentType.Contest:
                case DocumentType.GameSummary:
                case DocumentType.Scoreboard:
                case DocumentType.Season:
                case DocumentType.TeamInformation:
                case DocumentType.Weeks:
                case DocumentType.Position:
                case DocumentType.AthleteImage:
                default:
                    throw new ArgumentOutOfRangeException(nameof(documentType), documentType, null);
            }
        }
    }
}
