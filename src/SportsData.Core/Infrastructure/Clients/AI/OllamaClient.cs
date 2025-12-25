using FluentValidation.Results;

using Microsoft.Extensions.Logging;

using SportsData.Core.Common;

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

        public async Task<Result<string>> GetResponseAsync(
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

                _logger.LogInformation("OllamaClient began. {@Config} {@Request}", _config, request);

                _httpClient.DefaultRequestHeaders.Add("ngrok-skip-browser-warning", "true");

                using var response = await _httpClient.PostAsJsonAsync("/api/generate", request, ct);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Ollama returned non-success status code: {StatusCode}", response.StatusCode);
                    return new Failure<string>(string.Empty, ResultStatus.Error, [new ValidationFailure("AI", $"Ollama returned non-success status code: {response.StatusCode}")]);
                }

                using var contentStream = await response.Content.ReadAsStreamAsync(ct);

                var payload = await JsonSerializer.DeserializeAsync<OllamaGenerateResponse>(
                    contentStream,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                    ct
                );

                return new Success<string>(payload?.Response?.Trim() ?? string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve response from Ollama.");
                return new Failure<string>(string.Empty, ResultStatus.Error, [new ValidationFailure("AI", "Failed to retrieve response from Ollama")]);
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
            var response = await GetResponseAsync(prompt, ct);

            if (!response.IsSuccess || string.IsNullOrWhiteSpace(response.Value))
                return default;

            try
            {
                return JsonSerializer.Deserialize<T>(response.Value, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize Ollama response into {Type}", typeof(T).Name);
                _logger.LogDebug("Raw response was: {Raw}", response.Value);
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
