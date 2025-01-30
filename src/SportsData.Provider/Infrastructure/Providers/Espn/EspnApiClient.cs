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

        public async Task<EspnResourceIndexDto> GetResourceIndex(string uri, string? uriMask)
        {
            var dto = await GetAsync<EspnResourceIndexDto>(uri, true);

            return ExtractIds(dto, uriMask);
        }

        public EspnResourceIndexDto ExtractIds(EspnResourceIndexDto dto, string? uriMask)
        {
            if (string.IsNullOrEmpty(uriMask))
            {
                // TODO: Work this as a span in-memory (no string allocs)
                dto.items.ForEach(i =>
                {
                    var qsIndex = i.href.IndexOf("?");

                    var tmpUrl = i.href.Remove(qsIndex, i.href.Length - qsIndex);
                    var lastSlashIndex = tmpUrl.LastIndexOf("/");

                    tmpUrl = tmpUrl.Remove(0, lastSlashIndex + 1);

                    if (int.TryParse(tmpUrl, out var indexItemId))
                    {
                        i.id = indexItemId;
                    }
                });
            }
            else
            {
                const string mask1 = "?lang=en";

                dto.items.ForEach(i =>
                {
                    var url = i.href;
                    url = url.Replace(uriMask, string.Empty);
                    url = url.Replace(mask1, string.Empty);

                    if (int.TryParse(url, out var indexItemId))
                    {
                        i.id = indexItemId;
                    }
                });
            }

            return dto;
        }

        public async Task<string> GetResource(string uri, bool ignoreCache)
        {
            var response = await base.GetAsync(uri);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
    }
}
