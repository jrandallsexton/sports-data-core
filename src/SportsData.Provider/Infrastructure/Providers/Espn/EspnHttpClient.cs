using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;

namespace SportsData.Provider.Infrastructure.Providers.Espn
{
    public class EspnHttpClient
    {
        private readonly HttpClient _httpClient;
        private readonly EspnApiClientConfig _config;
        private readonly ILogger<EspnHttpClient> _logger;

        public EspnHttpClient(HttpClient httpClient,
                              EspnApiClientConfig config,
                              ILogger<EspnHttpClient> logger)
        {
            _httpClient = httpClient;
            _config = config;
            _logger = logger;
        }

        public async Task<string> GetRawJsonAsync(Uri uri, bool bypassCache = false)
        {
            // Check cache first
            if (_config is { ReadFromCache: true, ForceLiveFetch: false } && !bypassCache)
            {
                var cached = await TryLoadFromDiskAsync(uri);
                if (!string.IsNullOrEmpty(cached))
                {
                    _logger.LogInformation("Cache HIT for {Uri}", uri);
                    return cached;
                }

                _logger.LogInformation("Cache MISS for {Uri}", uri);
            }

            // Make HTTP call
            _logger.LogInformation("Fetching LIVE from ESPN: {Uri}", uri);

            // TODO: Make this delay configurable via Azure App Settings
            // prevent banging on ESPN API too fast
            await Task.Delay(250);

            var response = await _httpClient.GetAsync(uri);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Non-success status from ESPN: {StatusCode} for {Uri}", response.StatusCode, uri);
                return string.Empty;
            }

            var json = await response.Content.ReadAsStringAsync();

            // Optionally persist
            if (_config.PersistLocally && !bypassCache)
            {
                await SaveToDiskAsync(uri, json);
            }

            return json;
        }


        public async Task<T?> GetDeserializedAsync<T>(Uri uri, bool bypassCache = false) where T : class
        {
            var json = await GetRawJsonAsync(uri, bypassCache);

            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning("Empty JSON returned for {Uri}", uri);
                return null;
            }

            try
            {
                return json.FromJson<T>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize JSON for {Uri}", uri);
                throw;
            }
        }

        private async Task<string?> TryLoadFromDiskAsync(Uri uri)
        {
            var path = GetCacheFilePath(uri);

            if (File.Exists(path))
            {
                return await File.ReadAllTextAsync(path);
            }

            return null;
        }

        private async Task SaveToDiskAsync(Uri uri, string json)
        {
            var path = GetCacheFilePath(uri);

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await File.WriteAllTextAsync(path, json);

            _logger.LogInformation("Persisted JSON to {Path}", path);
        }

        private string GetCacheFilePath(Uri uri)
        {
            var filename = ConvertUriToFilename(uri) + ".json";
            return Path.Combine(_config.LocalCacheDirectory, filename);
        }

        private static string ConvertUriToFilename(Uri uri)
        {
            return HashProvider.GenerateHashFromUri(uri);
        }
    }
}
