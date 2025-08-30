using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SportsData.Core.Infrastructure.DataSources.Espn;
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
        public async Task<string> GetResource(Uri uri, bool bypassCache = false, bool stripQuerystring = true)
        {
            _logger.LogDebug("GetResource called for URI: {Uri}", uri);

            return await _http.GetRawJsonAsync(uri, bypassCache, stripQuerystring);
        }

        /// <summary>
        /// Strongly-typed fetch for any ESPN ResourceIndex document.
        /// </summary>
        public async Task<EspnResourceIndexDto> GetResourceIndex(Uri uri, string? uriMask)
        {
            _logger.LogDebug("GetResourceIndex called for URI: {Uri}", uri);
            var dto = await _http.GetDeserializedAsync<EspnResourceIndexDto>(uri, true, false);

            if (dto is not null)
                return ExtractIds(dto, uriMask);

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

        /// <summary>
        /// Post-processing for ESPN ResourceIndex items.
        /// Extracts ID from Ref.AbsoluteUri using optional URI mask.
        /// </summary>
        public EspnResourceIndexDto ExtractIds(EspnResourceIndexDto dto, string? uriMask)
        {
            if (dto.Items.Count == 0)
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
                        i.Id = tmpUrl.Substring(lastSlashIndex + 1);
                    }
                }
            }
            else
            {
                const string mask1 = "?lang=en";

                foreach (var i in dto.Items)
                {
                    i.Id = i.Ref.AbsoluteUri
                                  .Replace(uriMask, string.Empty)
                                  .Replace(mask1, string.Empty);
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
            return await _http.GetDeserializedAsync<EspnTeamSeasonDto>(uri, false, false);
        }

        public async Task<EspnEventCompetitionPlaysDto?> GetCompetitionPlaysAsync(Uri uri)
        {
            _logger.LogDebug("Fetching CompetitionPlays from {Uri}", uri);
            return await _http.GetDeserializedAsync<EspnEventCompetitionPlaysDto>(uri, bypassCache: false);
        }

        public async Task<EspnEventCompetitionStatusDto?> GetCompetitionStatusAsync(Uri uri)
        {
            _logger.LogDebug("Fetching CompetitionStatus from {Uri}", uri);
            return await _http.GetDeserializedAsync<EspnEventCompetitionStatusDto>(uri, bypassCache: false);
        }

    }
}
