using Newtonsoft.Json;

using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Provider.Infrastructure.Providers.Espn.DTOs.Athlete;
using SportsData.Provider.Infrastructure.Providers.Espn.DTOs.Award;
using SportsData.Provider.Infrastructure.Providers.Espn.DTOs.Franchise;
using SportsData.Provider.Infrastructure.Providers.Espn.DTOs.ResourceIndex;
using SportsData.Provider.Infrastructure.Providers.Espn.DTOs.TeamInformation;

using Team = SportsData.Provider.Infrastructure.Providers.Espn.DTOs.Team.Team;

namespace SportsData.Provider.Infrastructure.Providers.Espn
{
    public class EspnApiClient : EspnHttpClientBase, IProvideEspnApiData
    {
        public EspnApiClient(ILogger<EspnHttpClientBase> logger, EspnApiClientConfig config) :
            base(logger, config)
        { }

        public async Task<ResourceIndex> Awards(int franchiseId)
        {
            return await GetAsync<ResourceIndex>(EspnApiEndpoints.Awards(franchiseId));
        }

        public async Task<List<Award>> AwardsByFranchise(int franchiseId)
        {
            var franchiseAwards = await GetAsync<ResourceIndex>(EspnApiEndpoints.Awards(franchiseId));
            if (franchiseAwards == null || franchiseAwards.count == 0)
                return new List<Award>();

            var awards = new List<Award>();

            await franchiseAwards.items.ForEachAsync(async i =>
            {
                var award = await GetAward(i.href);
                awards.Add(award);
                await Task.Delay(1000);
            });

            return awards;
        }

        private async Task<Award> GetAward(string uri)
        {
            var award = await GetAsync<Award>(uri);

            await award.Winners.Where(w => w.Athlete != null).ToList().ForEachAsync(async w =>
            {
                await GetAthlete(w.Athlete.Ref?.AbsoluteUri);
            });
            return award;
        }

        private async Task<Athlete> GetAthlete(string uri)
        {
            return await GetAsync<Athlete>(uri);
        }

        public async Task<Franchise> Franchise(int franchiseId)
        {
            var franchise = await GetAsync<Franchise>(EspnApiEndpoints.Franchise(franchiseId));
            return franchise;
        }

        public async Task<ResourceIndex> Franchises()
        {
            var franchises = await GetAsync<ResourceIndex>(EspnApiEndpoints.Franchises);

            var mask0 = $"http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/franchises/";
            const string mask1 = "?lang=en";
            franchises.items.ForEach(i =>
            {
                var url = i.href;
                url = url.Replace(mask0, string.Empty);
                url = url.Replace(mask1, string.Empty);

                int.TryParse(url, out var franchiseId);

                i.id = franchiseId;
            });

            return franchises;
        }

        public async Task<Team> EspnTeam(int fourDigitYear, int teamId)
        {
            using var response = await GetAsync(EspnApiEndpoints.Team(fourDigitYear, teamId));
            var venuesJson = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<Team>(venuesJson, JsonSerializerSettings);
        }

        public async Task<TeamInformation> TeamInformation(int teamId)
        {
            using var response = await GetAsync(EspnApiEndpoints.TeamInformation(teamId));
            var venuesJson = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<TeamInformation>(venuesJson, JsonSerializerSettings);
        }

        public async Task<ResourceIndex> Teams(int fourDigitYear)
        {
            using var response = await GetAsync(EspnApiEndpoints.Teams(fourDigitYear));
            var venuesJson = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<ResourceIndex>(venuesJson, JsonSerializerSettings);
        }

        public async Task<ResourceIndex> Venues(bool ignoreCache)
        {
            var venues = await GetAsync<ResourceIndex>(EspnApiEndpoints.Venues, ignoreCache);

            var mask0 = $"http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/venues/";
            const string mask1 = "?lang=en";
            venues.items.ForEach(i =>
            {
                var url = i.href;
                url = url.Replace(mask0, string.Empty);
                url = url.Replace(mask1, string.Empty);

                int.TryParse(url, out var venueId);

                i.id = venueId;
            });

            return venues;
        }

        public async Task<EspnVenueDto> Venue(int venueId, bool ignoreCache)
        {
            return await GetAsync<EspnVenueDto>(EspnApiEndpoints.Venue(venueId), ignoreCache);
        }
    }
}
