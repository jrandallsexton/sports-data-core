using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Provider.Infrastructure.Providers.Espn
{
    public class EspnApiClient : IProvideEspnApiData
    {
        private readonly EspnHttpClient _http;
        private readonly ILogger<EspnApiClient> _logger;

        public EspnApiClient(EspnHttpClient http, ILogger<EspnApiClient> logger)
        {
            _http = http;
            _logger = logger;
        }

        /// <summary>
        /// Generic raw JSON fetch for any ESPN resource URI.
        /// </summary>
        public async Task<string> GetResource(Uri uri)
        {
            _logger.LogInformation("GetResource called for URI: {Uri}", uri);
            return await _http.GetRawJsonAsync(uri);
        }

        /// <summary>
        /// Strongly-typed fetch for any ESPN ResourceIndex document.
        /// </summary>
        public async Task<EspnResourceIndexDto> GetResourceIndex(Uri uri, string? uriMask)
        {
            _logger.LogInformation("GetResourceIndex called for URI: {Uri}", uri);
            var dto = await _http.GetDeserializedAsync<EspnResourceIndexDto>(uri);

            if (dto is null)
            {
                _logger.LogWarning("Null or empty ResourceIndex returned for {Uri}", uri);
                return new EspnResourceIndexDto
                {
                    Count = 0,
                    Items = [],
                    PageCount = 0,
                    PageIndex = 0,
                    PageSize = 0
                };
            }

            return ExtractIds(dto, uriMask);
        }

        /// <summary>
        /// Post-processing for ESPN ResourceIndex items.
        /// Extracts ID from Ref.AbsoluteUri using optional URI mask.
        /// </summary>
        public EspnResourceIndexDto ExtractIds(EspnResourceIndexDto dto, string? uriMask)
        {
            if (dto.Items == null || dto.Items.Count == 0)
            {
                return dto;
            }

            if (string.IsNullOrEmpty(uriMask))
            {
                foreach (var i in dto.Items)
                {
                    var qsIndex = i.Ref.AbsoluteUri.IndexOf('?');
                    var tmpUrl = (qsIndex > 0)
                        ? i.Ref.AbsoluteUri.Substring(0, qsIndex)
                        : i.Ref.AbsoluteUri;

                    var lastSlashIndex = tmpUrl.LastIndexOf('/');
                    if (lastSlashIndex >= 0)
                    {
                        var idPart = tmpUrl.Substring(lastSlashIndex + 1);
                        if (int.TryParse(idPart, out var id))
                        {
                            i.Id = id;
                        }
                    }
                }
            }
            else
            {
                const string mask1 = "?lang=en";

                foreach (var i in dto.Items)
                {
                    var url = i.Ref.AbsoluteUri
                                  .Replace(uriMask, string.Empty)
                                  .Replace(mask1, string.Empty);

                    if (int.TryParse(url, out var id))
                    {
                        i.Id = id;
                    }
                }
            }

            return dto;
        }

        /// <summary>
        /// Example strongly-typed DTO fetch.
        /// Add as many as you want here.
        /// </summary>
        public async Task<EspnTeamSeasonDto?> GetTeamSeason(Uri uri)
        {
            _logger.LogInformation("GetTeamSeason called for URI: {Uri}", uri);
            return await _http.GetDeserializedAsync<EspnTeamSeasonDto>(uri);
        }

        // You can add more typed methods here:
        // public async Task<AwardDto?> GetAward(string uri) { ... }
        // public async Task<EspnAthleteDto?> GetAthlete(string uri) { ... }
        // etc.
    }
}
