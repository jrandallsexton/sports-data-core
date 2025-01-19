using SportsData.Core.Common;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;

namespace SportsData.Provider.Application.Documents
{
    public interface IDecodeDocumentProvidersAndTypes
    {
        Type GetType(SourceDataProvider sourceDataProvider, DocumentType docType);
        (Type Type, string Name) GetTypeAndName(SourceDataProvider sourceDataProvider, Sport sport, DocumentType docType, int? season);
    }

    public class DocumentProviderAndTypeDecoder : IDecodeDocumentProvidersAndTypes
    {
        public Type GetType(SourceDataProvider sourceDataProvider, DocumentType docType)
        {
            switch (docType)
            {
                case DocumentType.Franchise:
                    return typeof(EspnFranchiseDto);
                case DocumentType.TeamBySeason:
                    return typeof(EspnTeamSeasonDto);
                case DocumentType.Venue:
                    return typeof(EspnVenueDto);
                case DocumentType.CoachBySeason:
                    return typeof(EspnCoachBySeasonDto);
                case DocumentType.Athlete:
                    return typeof(EspnAthleteDto);
                case DocumentType.GroupBySeason:
                    return typeof(EspnGroupBySeasonDto);
                case DocumentType.Award:
                case DocumentType.Contest:
                    // TODO: Create these => return typeof(EspnContestDto);
                case DocumentType.GameSummary:
                case DocumentType.Scoreboard:
                case DocumentType.Season:
                case DocumentType.Team:
                case DocumentType.TeamInformation:
                case DocumentType.Weeks:
                default:
                    throw new ArgumentOutOfRangeException(nameof(docType), docType, null);
            }
        }

        public (Type, string) GetTypeAndName(SourceDataProvider sourceDataProvider, Sport sport, DocumentType docType, int? season)
        {
            var name = season.HasValue ?
                $"{sourceDataProvider.ToString()}{sport.ToString()}{docType.ToString()}{season.Value}" :
                $"{sourceDataProvider.ToString()}{sport.ToString()}{docType.ToString()}";

            switch (docType)
            {
                case DocumentType.Franchise:
                    return (typeof(EspnFranchiseDto), name);
                case DocumentType.TeamBySeason:
                    return (typeof(EspnTeamSeasonDto), name);
                case DocumentType.Venue:
                    return (typeof(EspnVenueDto), name);
                case DocumentType.CoachBySeason:
                    return (typeof(EspnCoachBySeasonDto), name);
                case DocumentType.Athlete:
                    return (typeof(EspnAthleteDto), name);
                case DocumentType.GroupBySeason:
                    return (typeof(EspnGroupBySeasonDto), name);
                case DocumentType.Award:
                case DocumentType.Contest:
                // TODO: Create these => return typeof(EspnContestDto);
                case DocumentType.GameSummary:
                case DocumentType.Scoreboard:
                case DocumentType.Season:
                case DocumentType.Team:
                case DocumentType.TeamInformation:
                case DocumentType.Weeks:
                default:
                    throw new ArgumentOutOfRangeException(nameof(docType), docType, null);
            }
        }
    }
}
