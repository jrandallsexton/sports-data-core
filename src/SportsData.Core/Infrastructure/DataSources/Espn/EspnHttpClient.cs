using FluentValidation.Results;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.DataSources.Espn
{
    public class EspnHttpClient
    {
        private readonly HttpClient _httpClient;
        private readonly IOptionsMonitor<EspnApiClientConfig> _configMonitor;
        private readonly ILogger<EspnHttpClient> _logger;
        private readonly IEspnCircuitBreaker _circuitBreaker;
        private readonly IEspnRateLimiter _rateLimiter;

        public EspnHttpClient(HttpClient httpClient,
                              IOptionsMonitor<EspnApiClientConfig> config,
                              ILogger<EspnHttpClient> logger,
                              IEspnCircuitBreaker circuitBreaker,
                              IEspnRateLimiter rateLimiter)
        {
            _httpClient = httpClient;
            _configMonitor = config;
            _logger = logger;
            _circuitBreaker = circuitBreaker;
            _rateLimiter = rateLimiter;
        }

        public async Task<Result<string>> GetRawJsonAsync(
            Uri uri,
            bool bypassCache,
            bool stripQuerystring = true)
        {
            var config = _configMonitor.CurrentValue;

            // Check cache first
            if (config is { ReadFromCache: true, ForceLiveFetch: false } && !bypassCache)
            {
                var cached = await TryLoadFromDiskAsync(config, uri, stripQuerystring);
                if (!string.IsNullOrEmpty(cached))
                {
                    _logger.LogDebug("Cache HIT for {Uri}", uri);

                    // Treat literal "null" string as invalid cache
                    if (cached.Trim() == "null")
                    {
                        _logger.LogWarning("Cached data is literal 'null' for {Uri}, will fetch live", uri);
                        return await FetchLiveAsync(config, uri, bypassCache, stripQuerystring);
                    }

                    try
                    {
                        JsonDocument.Parse(cached).Dispose();
                        return new Success<string>(cached);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Cached JSON is invalid for {Uri}, will fetch live", uri);
                        return await FetchLiveAsync(config, uri, bypassCache, stripQuerystring);
                    }
                }

                _logger.LogDebug("Cache MISS for {Uri}", uri);
            }

            return await FetchLiveAsync(config, uri, bypassCache, stripQuerystring);
        }

        private async Task<Result<string>> FetchLiveAsync(EspnApiClientConfig config, Uri uri, bool bypassCache, bool stripQuerystring)
        {
            // Check circuit breaker before making any ESPN call
            if (await _circuitBreaker.IsOpenAsync())
            {
                _logger.LogDebug("ESPN circuit breaker is open, skipping request for {Uri}", uri);
                return new Failure<string>(
                    default!,
                    ResultStatus.RateLimited,
                    [new ValidationFailure(nameof(uri), "ESPN circuit breaker is open — rate limited")]);
            }

            // Request-only HTTPS upgrade
            var requestUri = EspnRequestUri.ForFetch(uri);

            _logger.LogDebug("Fetching LIVE from ESPN: {RequestUri} (identity: {IdentityUri})", requestUri, uri);

            // Centralized rate limiting — blocks until a token is available
            await _rateLimiter.AcquireAsync();

            try
            {
                using var response = await _httpClient.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead);

                if (!response.IsSuccessStatusCode)
                {
                    var status = response.StatusCode switch
                    {
                        HttpStatusCode.BadRequest => ResultStatus.BadRequest,
                        HttpStatusCode.Unauthorized => ResultStatus.Unauthorized,
                        HttpStatusCode.Forbidden => ResultStatus.Forbid,
                        HttpStatusCode.NotFound => ResultStatus.NotFound,
                        (HttpStatusCode)429 => ResultStatus.RateLimited, // TooManyRequests
                        HttpStatusCode.ServiceUnavailable => ResultStatus.Error,
                        _ => ResultStatus.Error
                    };
                    
                    if (status == ResultStatus.Forbid)
                    {
                        await _circuitBreaker.TripAsync($"ESPN returned 403 for {uri}");
                    }

                    _logger.LogError(
                        "ESPN API returned {StatusCode} for {Uri}",
                        response.StatusCode,
                        uri);

                    return new Failure<string>(
                        default!,
                        status,
                        [new ValidationFailure(nameof(uri), $"ESPN API returned {response.StatusCode} for {uri}")]);
                }

                var json = await response.Content.ReadAsStringAsync();

                // Validate response
                if (string.IsNullOrWhiteSpace(json) || json.Trim() == "null")
                {
                    _logger.LogError(
                        "ESPN returned empty/null response for {Uri}",
                        uri);
                    
                    return new Failure<string>(
                        default!,
                        ResultStatus.BadRequest,
                        [new ValidationFailure(nameof(uri), $"ESPN returned empty/null response for {uri}")]);
                }
                
                // Validate JSON
                try
                {
                    JsonDocument.Parse(json).Dispose();
                }
                catch (JsonException ex)
                {
                    _logger.LogError(
                        ex,
                        "ESPN returned invalid JSON for {Uri}. Response: {Response}",
                        uri,
                        json.Length > 200 ? json.Substring(0, 200) + "..." : json);
                    
                    return new Failure<string>(
                        default!,
                        ResultStatus.BadRequest,
                        [new ValidationFailure(nameof(uri), $"ESPN returned invalid JSON for {uri}")]);
                }

                // Optionally persist
                if (config.PersistLocally && !bypassCache)
                {
                    // Persist under the ORIGINAL identity URI key
                    await SaveToDiskAsync(config, uri, json, stripQuerystring);
                }

                return new Success<string>(json);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed for {Uri}", uri);
                return new Failure<string>(
                    default!,
                    ResultStatus.Error,
                    [new ValidationFailure(nameof(uri), $"HTTP request failed: {ex.Message}")]);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "HTTP request timed out for {Uri}", uri);
                return new Failure<string>(
                    default!,
                    ResultStatus.Error,
                    [new ValidationFailure(nameof(uri), $"HTTP request timed out: {ex.Message}")]);
            }
        }


        public async Task<T?> GetDeserializedAsync<T>(Uri uri, bool bypassCache, bool stripQuerystring = true) where T : class
        {
            var result = await GetRawJsonAsync(uri, bypassCache, stripQuerystring);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to get JSON for {Uri}: {Status}", uri, result.Status);
                return null;
            }

            try
            {
                return result.Value.FromJson<T>();
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
            var config = _configMonitor.CurrentValue;
            var path = GetCacheFilePath(config, uri, stripQuerystring, extension);

            if (!bypassCache && config.ReadFromCache && !config.ForceLiveFetch)
            {
                if (File.Exists(path))
                {
                    _logger.LogDebug("Cache HIT for image {Uri}", uri.ToString().Sanitize());
                    return File.OpenRead(path);
                }

                _logger.LogDebug("Cache MISS for image {Uri}", uri.ToString().Sanitize());
            }

            // Check circuit breaker before making any ESPN call
            if (await _circuitBreaker.IsOpenAsync())
            {
                _logger.LogDebug("ESPN circuit breaker is open, skipping image request for {Uri}", uri.ToString().Sanitize());
                return null;
            }

            _logger.LogInformation("Fetching image from {Uri}", uri.ToString().Sanitize());
            await _rateLimiter.AcquireAsync(ct);

            using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch image from {Uri}, status {StatusCode}", uri.ToString().Sanitize(), (int)response.StatusCode);
                return null;
            }

            await using var networkStream = await response.Content.ReadAsStreamAsync(ct);

            if (bypassCache)
            {
                _logger.LogDebug("Bypassing cache, returning raw stream for {Uri}", uri.ToString().Sanitize());
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

        private async Task<string?> TryLoadFromDiskAsync(EspnApiClientConfig config, Uri uri, bool stripQuerystring = true)
        {
            var path = GetCacheFilePath(config, uri, stripQuerystring);
            
            if (File.Exists(path))
            {
                return await File.ReadAllTextAsync(path);
            }

            return null;
        }

        private async Task SaveToDiskAsync(EspnApiClientConfig config, Uri uri, string json, bool stripQuerystring = true)
        {
            var path = GetCacheFilePath(config, uri, stripQuerystring);

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

        private static string GetCacheFilePath(EspnApiClientConfig config, Uri uri, bool stripQuerystring = true, string extension = "json")
        {
            var filename = ConvertUriToFilename(uri, stripQuerystring) + $".{extension}";
            return Path.Combine(config.LocalCacheDirectory, filename);
        }

        private static string ConvertUriToFilename(Uri uri, bool stripQuerystring = true)
        {
            return HashProvider.GenerateHashFromUri(uri, stripQuerystring);
        }
    }
}
