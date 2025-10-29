using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.YouTube.Dtos;

using System.Net.Http;
using System.Threading.Tasks;
using System.Web;

namespace SportsData.Core.Infrastructure.Clients.YouTube
{
    public interface IProvideYouTube
    {
        Task<YouTubeSearchResultDto?> Search(string query);
    }

    public class YouTubeHttpClient : ClientBase, IProvideYouTube
    {
        private readonly YouTubeClientConfig _config;
        private readonly ILogger<YouTubeHttpClient> _logger;

        public YouTubeHttpClient(
            ILogger<YouTubeHttpClient> logger,
            HttpClient httpClient,
            IOptions<YouTubeClientConfig> config)
            : base(httpClient)
        {
            _logger = logger;
            _config = config.Value;
        }

        public async Task<YouTubeSearchResultDto?> Search(string query)
        {
            _logger.LogInformation("YouTube query {Query}", HttpUtility.UrlEncode(query));

            var url = $"{_config.BaseUrl}/search?part=snippet" +
                      $"&q={HttpUtility.UrlEncode(query)}" +
                      $"&channelId={_config.DefaultChannelId}" +
                      $"&type=video&order=date&maxResults=5" +
                      $"&key={_config.ApiKey}";

            var response = await HttpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var result = json.FromJson<YouTubeSearchResultDto>();

            _logger.LogInformation("YouTube response {@Response}", result);

            return result;
        }
    }

}
