using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;

namespace SportsData.Provider.Infrastructure.Providers.Espn
{
    public interface IProvideEspnApiData
    {
        //Task<EspnResourceIndexDto> Awards(int franchiseId);

        //Task<List<Award>> AwardsByFranchise(int franchiseId);

        //Task<EspnTeamSeasonDto> EspnTeam(int fourDigitYear, int teamId);

        //Task<TeamInformation> TeamInformation(int teamId);

        //Task<EspnResourceIndexDto> Teams(int fourDigitYear);

        //Task<byte[]> GetMedia(string uri);

        Task<EspnResourceIndexDto> GetResourceIndex(Uri uri, string? uriMask);

        Task<string> GetResource(Uri uri, bool stripQuerystring = true);
    }
}
