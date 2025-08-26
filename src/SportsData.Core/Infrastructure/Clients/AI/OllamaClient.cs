using Microsoft.Extensions.Logging;

using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.Clients.AI
{
    public class OllamaClient : IProvideAiCommunication
    {
        private readonly HttpClient _httpClient;
        private readonly OllamaClientConfig _config;
        private readonly ILogger<OllamaClient> _logger;

        private static readonly SemaphoreSlim _lock = new(1, 1); // global throttle for now

        public OllamaClient(HttpClient httpClient,
                            OllamaClientConfig config,
                            ILogger<OllamaClient> logger)
        {
            _httpClient = httpClient;
            _config = config;
            _logger = logger;
        }

        public async Task<string> GetResponseAsync(
            string prompt,
            CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct); // ⬅️ acquire lock
            try
            {
                var request = new
                {
                    model = _config.Model,
                    prompt = prompt,
                    stream = false
                };

                _logger.LogDebug("OllamaClient began. {@Config} {@Request}", _config, request);

                _httpClient.DefaultRequestHeaders.Add("ngrok-skip-browser-warning", "true");

                using var response = await _httpClient.PostAsJsonAsync("/api/generate", request, ct);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Ollama returned non-success status code: {StatusCode}", response.StatusCode);
                    return string.Empty;
                }

                using var contentStream = await response.Content.ReadAsStreamAsync(ct);

                var payload = await JsonSerializer.DeserializeAsync<OllamaGenerateResponse>(
                    contentStream,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                    ct
                );

                return payload?.Response?.Trim() ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve response from Ollama.");
                return string.Empty;
            }
            finally
            {
                _lock.Release(); // ⬅️ always release
            }
        }

        public async Task<T?> GetTypedResponseAsync<T>(
            string prompt,
            CancellationToken ct = default)
        {
            var rawResponse = await GetResponseAsync(prompt, ct);

            if (string.IsNullOrWhiteSpace(rawResponse))
                return default;

            try
            {
                return JsonSerializer.Deserialize<T>(rawResponse, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize Ollama response into {Type}", typeof(T).Name);
                _logger.LogDebug("Raw response was: {Raw}", rawResponse);
                return default;
            }
        }

        public string GetModelName()
        {
            return _config.Model;
        }

        private class OllamaGenerateResponse
        {
            public required string Response { get; set; }
        }
    }

    public class OllamaClientConfig
    {
        public required string Model { get; set; }
        public required string BaseUrl { get; set; }
    }
}
