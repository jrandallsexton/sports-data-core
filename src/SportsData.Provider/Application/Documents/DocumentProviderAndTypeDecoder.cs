using SportsData.Core.Common;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;

namespace SportsData.Provider.Application.Documents
{
    public interface IDecodeDocumentProvidersAndTypes
    {
        Type GetType(SourceDataProvider sourceDataProvider, DocumentType docType);
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
                case DocumentType.Athlete:
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
