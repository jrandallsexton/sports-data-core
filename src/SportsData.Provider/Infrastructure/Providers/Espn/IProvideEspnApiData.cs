using SportsData.Provider.Infrastructure.Providers.Espn.DTOs.Award;
using SportsData.Provider.Infrastructure.Providers.Espn.DTOs.Franchise;
using SportsData.Provider.Infrastructure.Providers.Espn.DTOs.ResourceIndex;
using SportsData.Provider.Infrastructure.Providers.Espn.DTOs.TeamInformation;

using Venue = SportsData.Provider.Infrastructure.Providers.Espn.DTOs.Venue.Venue;

namespace SportsData.Provider.Infrastructure.Providers.Espn
{
    public interface IProvideEspnApiData
    {
        Task<ResourceIndex> Awards(int franchiseId);
        Task<List<Award>> AwardsByFranchise(int franchiseId);
        Task<Franchise> Franchise(int franchiseId);
        Task<ResourceIndex> Franchises();
        Task<Infrastructure.Providers.Espn.DTOs.Team.Team> EspnTeam(int fourDigitYear, int teamId);
        Task<TeamInformation> TeamInformation(int teamId);
        Task<ResourceIndex> Teams(int fourDigitYear);
        Task<ResourceIndex> Venues();
        Task<Venue> Venue(int venueId, bool ignoreCache);
        Task<byte[]> GetMedia(string uri);
    }
}
