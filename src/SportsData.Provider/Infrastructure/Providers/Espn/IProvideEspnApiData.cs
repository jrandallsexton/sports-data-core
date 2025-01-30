using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Provider.Infrastructure.Providers.Espn.DTOs.Award;
using SportsData.Provider.Infrastructure.Providers.Espn.DTOs.TeamInformation;

namespace SportsData.Provider.Infrastructure.Providers.Espn
{
    public interface IProvideEspnApiData
    {
        Task<EspnResourceIndexDto> Awards(int franchiseId);

        Task<List<Award>> AwardsByFranchise(int franchiseId);

        Task<EspnTeamSeasonDto> EspnTeam(int fourDigitYear, int teamId);

        Task<TeamInformation> TeamInformation(int teamId);

        Task<EspnResourceIndexDto> Teams(int fourDigitYear);

        Task<byte[]> GetMedia(string uri);

        Task<EspnResourceIndexDto> GetResourceIndex(string uri, string? uriMask);

        Task<string> GetResource(string uri, bool ignoreCache);
    }
}
