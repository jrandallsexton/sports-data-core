
using SportsData.Core.Extensions;

namespace SportsData.Provider.Infrastructure.Providers.Espn
{
    public class EspnHttpClientBase : HttpClient
    {
        private readonly ILogger<EspnHttpClientBase> _logger;
        private readonly EspnApiClientConfig _config;

        protected EspnHttpClientBase(ILogger<EspnHttpClientBase> logger, EspnApiClientConfig config)
        {
            _logger = logger;
            _config = config;
        }

        public async Task<T> GetAsync<T>(string uri, bool ignoreCache = false) where T : class
        {
            if (!ignoreCache)
            {
                var cachedJson = await GetJsonFromFile(uri);

                if (!string.IsNullOrEmpty(cachedJson))
                {
                    return cachedJson.FromJson<T>();
                }
            }

            _logger?.LogInformation("Beginning call to {uri}", uri);
            using var response = await base.GetAsync(uri);
            var responseJson = await response.Content.ReadAsStringAsync();

            //await PersistJsonToDisk(uri, responseJson);

            return responseJson.FromJson<T>();
        }

        public async Task<byte[]> GetMedia(string uri)
        {
            // TODO: Look for local version prior to downloading via HTTP

            _logger?.LogInformation("Beginning call to {uri}", uri);

            using var response = await GetAsync(uri);
            var contentBytes = await response.Content.ReadAsByteArrayAsync();

            await PersistMediaToDisk(uri, contentBytes);

            return contentBytes;
        }

        public new async Task<HttpResponseMessage> GetAsync(string uri)
        {
            _logger?.LogInformation("Beginning call to {uri}", uri);
            return await base.GetAsync(uri);
        }

        //public JsonSerializerSettings JsonSerializerSettings =>
        //    new JsonSerializerSettings
        //    {
        //        MetadataPropertyHandling = MetadataPropertyHandling.Ignore
        //    };

        private async Task PersistJsonToDisk(string uri, string jsonData)
        {
            uri = ConvertUriToFilename(uri);
            uri = $"{uri}.json";
            var path = Path.Combine(_config.DataDirectory, uri);
            await File.WriteAllTextAsync(path, jsonData);
        }

        private async Task<string> GetJsonFromFile(string uri)
        {
            var filename = ConvertUriToFilename(uri);
            filename = $"{filename}.json";
            var path = Path.Combine(_config.DataDirectory, filename);

            if (!File.Exists(path))
                return string.Empty;

            return await File.ReadAllTextAsync(path);
        }

        private async Task PersistMediaToDisk(string uri, byte[] mediaBytes)
        {
            var filename = ConvertUriToFilename(uri);
            var path = Path.Combine(_config.DataDirectory, filename);
            await File.WriteAllBytesAsync(path, mediaBytes);
        }

        private static string ConvertUriToFilename(string uri)
        {
            uri = uri.Replace("http://", string.Empty);
            uri = uri.Replace("https://", string.Empty);
            uri = uri.Replace("/", "-");
            uri = uri.Replace("=", "-");
            uri = uri.Replace("?", "-");
            return uri;
        }
    }
}
