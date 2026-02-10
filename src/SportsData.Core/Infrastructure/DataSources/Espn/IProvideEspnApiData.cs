using SportsData.Core.Common;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

using System;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.DataSources.Espn
{
    public interface IProvideEspnApiData
    {
        //Task<EspnResourceIndexDto> Awards(int franchiseId);

        //Task<List<Award>> AwardsByFranchise(int franchiseId);

        //Task<EspnTeamSeasonDto> EspnTeam(int fourDigitYear, int teamId);

        //Task<EspnResourceIndexDto> Teams(int fourDigitYear);

        //Task<byte[]> GetMedia(string uri);

        Task<EspnResourceIndexDto> GetResourceIndex(Uri uri, string? uriMask);

        Task<Result<string>> GetResource(Uri uri, bool bypassCache = false, bool stripQuerystring = true);

        Task<EspnEventCompetitionPlaysDto?> GetCompetitionPlaysAsync(Uri uri);

        Task<EspnEventCompetitionStatusDto?> GetCompetitionStatusAsync(Uri uri);
    }
}
