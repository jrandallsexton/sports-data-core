using Newtonsoft.Json;

using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Provider.Infrastructure.Providers.Espn.DTOs.Award;
using SportsData.Provider.Infrastructure.Providers.Espn.DTOs.TeamInformation;

namespace SportsData.Provider.Infrastructure.Providers.Espn
{
    public class EspnApiClient : EspnHttpClientBase, IProvideEspnApiData
    {
        public EspnApiClient(ILogger<EspnHttpClientBase> logger, EspnApiClientConfig config) :
            base(logger, config)
        { }

        public async Task<EspnResourceIndexDto> Awards(int franchiseId)
        {
            return await GetAsync<EspnResourceIndexDto>(EspnApiEndpoints.Awards(franchiseId));
        }

        public async Task<List<Award>> AwardsByFranchise(int franchiseId)
        {
            var franchiseAwards = await GetAsync<EspnResourceIndexDto>(EspnApiEndpoints.Awards(franchiseId));
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

        private async Task<EspnAthleteDto> GetAthlete(string uri)
        {
            return await GetAsync<EspnAthleteDto>(uri);
        }

        public async Task<EspnTeamSeasonDto> EspnTeam(int fourDigitYear, int teamId)
        {
            using var response = await GetAsync(EspnApiEndpoints.Team(fourDigitYear, teamId));
            var venuesJson = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<EspnTeamSeasonDto>(venuesJson, JsonSerializerSettings);
        }

        public async Task<TeamInformation> TeamInformation(int teamId)
        {
            using var response = await GetAsync(EspnApiEndpoints.TeamInformation(teamId));
            var venuesJson = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<TeamInformation>(venuesJson, JsonSerializerSettings);
        }

        public async Task<EspnResourceIndexDto> Teams(int fourDigitYear)
        {
            using var response = await GetAsync(EspnApiEndpoints.Teams(fourDigitYear));
            var venuesJson = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<EspnResourceIndexDto>(venuesJson, JsonSerializerSettings);
        }

        public async Task<EspnResourceIndexDto> GetResourceIndex(string uri, string uriMask)
        {
            var venues = await GetAsync<EspnResourceIndexDto>(uri, true);

            var mask0 = uriMask;
            const string mask1 = "?lang=en";
            venues.items.ForEach(i =>
            {
                var url = i.href;
                url = url.Replace(mask0, string.Empty);
                url = url.Replace(mask1, string.Empty);

                if (int.TryParse(url, out var venueId))
                {
                    i.id = venueId;
                }

            });

            return venues;
        }

        public async Task<string> GetResource(string uri, bool ignoreCache)
        {
            var response = await base.GetAsync(uri);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
    }
}
