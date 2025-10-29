using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;

using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.DataSources.Espn
{
    public class EspnHttpClient
    {
        private readonly HttpClient _httpClient;
        private readonly EspnApiClientConfig _config;
        private readonly ILogger<EspnHttpClient> _logger;

        public EspnHttpClient(HttpClient httpClient,
                              IOptions<EspnApiClientConfig> config,
                              ILogger<EspnHttpClient> logger)
        {
            _httpClient = httpClient;
            _config = config.Value;
            _logger = logger;
        }

        public async Task<string> GetRawJsonAsync(
            Uri uri,
            bool bypassCache,
            bool stripQuerystring = true)
        {
            // Check cache first
            if (_config is { ReadFromCache: true, ForceLiveFetch: false } && !bypassCache)
            {
                var cached = await TryLoadFromDiskAsync(uri, stripQuerystring);
                if (!string.IsNullOrEmpty(cached))
                {
                    _logger.LogDebug("Cache HIT for {Uri}", uri);
                    return cached;
                }

                _logger.LogDebug("Cache MISS for {Uri}", uri);
            }

            // Make HTTP call
            // Request-only HTTPS upgrade
            var requestUri = EspnRequestUri.ForFetch(uri);

            _logger.LogDebug("Fetching LIVE from ESPN: {RequestUri} (identity: {IdentityUri})", requestUri, uri);
            
            // prevent banging on ESPN API too fast
            await Task.Delay(_config.RequestDelayMs);

            using var response = await _httpClient.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Non-success status from ESPN: {StatusCode} for {Uri}", response.StatusCode, uri);
                return string.Empty;
            }

            var json = await response.Content.ReadAsStringAsync();

            // Optionally persist
            if (_config.PersistLocally && !bypassCache)
            {
                // Persist under the ORIGINAL identity URI key
                await SaveToDiskAsync(uri, json, stripQuerystring);
            }

            return json;
        }


        public async Task<T?> GetDeserializedAsync<T>(Uri uri, bool bypassCache, bool stripQuerystring = true) where T : class
        {
            var json = await GetRawJsonAsync(uri, bypassCache, stripQuerystring);

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

        public async Task<Stream?> GetCachedImageStreamAsync(
            Uri uri,
            bool bypassCache = false,
            bool stripQuerystring = true,
            string extension = "png",
            CancellationToken ct = default)
        {
            var path = GetCacheFilePath(uri, stripQuerystring, extension);

            if (!bypassCache && _config.ReadFromCache && !_config.ForceLiveFetch)
            {
                if (File.Exists(path))
                {
                    _logger.LogDebug("Cache HIT for image {Uri}", uri);
                    return File.OpenRead(path);
                }

                _logger.LogDebug("Cache MISS for image {Uri}", uri);
            }

            _logger.LogInformation("Fetching image from {Uri}", uri);
            await Task.Delay(_config.RequestDelayMs, ct);

            using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch image from {Uri}, status {StatusCode}", uri, (int)response.StatusCode);
                return null;
            }

            await using var networkStream = await response.Content.ReadAsStreamAsync(ct);

            if (bypassCache)
            {
                _logger.LogDebug("Bypassing cache, returning raw stream for {Uri}", uri);
                return await CopyToMemoryStreamAsync(networkStream, ct);
            }

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await using var fs = File.Create(path);
            await networkStream.CopyToAsync(fs, ct);

            _logger.LogInformation("Persisted image to {Path}", path);
            return File.OpenRead(path);
        }

        private static async Task<Stream> CopyToMemoryStreamAsync(Stream input, CancellationToken ct)
        {
            var ms = new MemoryStream();
            await input.CopyToAsync(ms, ct);
            ms.Position = 0;
            return ms;
        }

        private async Task<string?> TryLoadFromDiskAsync(Uri uri, bool stripQuerystring = true)
        {
            var path = GetCacheFilePath(uri, stripQuerystring);
            
            if (File.Exists(path))
            {
                return await File.ReadAllTextAsync(path);
            }

            return null;
        }

        private async Task SaveToDiskAsync(Uri uri, string json, bool stripQuerystring = true)
        {
            var path = GetCacheFilePath(uri, stripQuerystring);

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            const int maxRetries = 3;
            const int delayMilliseconds = 200;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    await using var fileStream = new FileStream(
                        path,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.ReadWrite, // 👈 Prevent locking conflicts (e.g., Dropbox, AV)
                        bufferSize: 4096,
                        useAsync: true
                    );

                    using var writer = new StreamWriter(fileStream);
                    await writer.WriteAsync(json);
                    _logger.LogInformation("Persisted JSON to {Path}", path);
                    return;
                }
                catch (IOException ex) when (attempt < maxRetries)
                {
                    _logger.LogWarning(ex, "File write conflict on attempt {Attempt} for {Path}, retrying...", attempt, path);
                    await Task.Delay(delayMilliseconds * attempt);
                }
            }

            throw new IOException($"Failed to write file after {maxRetries} attempts: {path}");
        }

        private string GetCacheFilePath(Uri uri, bool stripQuerystring = true, string extension = "json")
        {
            var filename = ConvertUriToFilename(uri, stripQuerystring) + $".{extension}";
            return Path.Combine(_config.LocalCacheDirectory, filename);
        }

        private static string ConvertUriToFilename(Uri uri, bool stripQuerystring = true)
        {
            return HashProvider.GenerateHashFromUri(uri, stripQuerystring);
        }
    }
}
