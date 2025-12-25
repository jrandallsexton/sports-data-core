using FluentValidation.Results;

using Microsoft.Extensions.Logging;

using SportsData.Core.Common;

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.Clients.AI
{
    /// <summary>
    /// Client for DeepSeek AI API - OpenAI-compatible chat completions endpoint
    /// </summary>
    public class DeepSeekClient : IProvideAiCommunication
    {
        private readonly HttpClient _httpClient;
        private readonly DeepSeekClientConfig _config;
        private readonly ILogger<DeepSeekClient> _logger;

        private static readonly SemaphoreSlim _lock = new(1, 1); // Global throttle

        public DeepSeekClient(
            HttpClient httpClient,
            DeepSeekClientConfig config,
            ILogger<DeepSeekClient> logger)
        {
            _httpClient = httpClient;
            _config = config;
            _logger = logger;

            // Configure HttpClient with auth header
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", _config.ApiKey);
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<Result<string>> GetResponseAsync(
            string prompt,
            CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct); // Acquire lock for rate limiting
            try
            {
                var request = new DeepSeekChatRequest
                {
                    Model = _config.Model,
                    Messages =
                    [
                        new DeepSeekMessage
                        {
                            Role = "user",
                            Content = prompt
                        }
                    ],
                    Temperature = _config.Temperature,
                    MaxTokens = _config.MaxTokens,
                    Stream = false,
                    FrequencyPenalty = 0,
                    PresencePenalty = 0,
                    TopP = 1
                };

                _logger.LogInformation(
                    "DeepSeekClient request started. Model: {Model}, MaxTokens: {MaxTokens}, Temperature: {Temperature}",
                    _config.Model,
                    _config.MaxTokens,
                    _config.Temperature);

                using var response = await _httpClient.PostAsJsonAsync(
                    _config.BaseUrl,
                    request,
                    new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull },
                    ct);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(ct);

                    if (errorContent.Contains("Insufficient Balance", StringComparison.OrdinalIgnoreCase))
                    {
                        var msg = $"DeepSeek API Balance Insufficient! Please top up the account. Error: {errorContent}";
                        _logger.LogCritical(msg);
                        return new Failure<string>(string.Empty, ResultStatus.Forbid, [new ValidationFailure("AI", msg)]);
                    }
                    else
                    {
                        _logger.LogError(
                            "DeepSeek returned non-success status code: {StatusCode}, Error: {Error}",
                            response.StatusCode,
                            errorContent);
                        return new Failure<string>(string.Empty, ResultStatus.Error, [new ValidationFailure("AI", $"DeepSeek returned non-success status code: {response.StatusCode}")]);
                    }
                }

                using var contentStream = await response.Content.ReadAsStreamAsync(ct);

                var chatResponse = await JsonSerializer.DeserializeAsync<DeepSeekChatResponse>(
                    contentStream,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                    ct);

                if (chatResponse?.Choices == null || chatResponse.Choices.Length == 0)
                {
                    _logger.LogError("DeepSeek returned empty or null choices");
                    return new Failure<string>(string.Empty, ResultStatus.Error, [new ValidationFailure("AI", "DeepSeek returned empty or null choices")]);
                }

                var content = chatResponse.Choices[0].Message?.Content?.Trim() ?? string.Empty;

                _logger.LogInformation(
                    "DeepSeek response received. Tokens - Prompt: {PromptTokens}, Completion: {CompletionTokens}, Total: {TotalTokens}",
                    chatResponse.Usage?.PromptTokens ?? 0,
                    chatResponse.Usage?.CompletionTokens ?? 0,
                    chatResponse.Usage?.TotalTokens ?? 0);

                return new Success<string>(content);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed when calling DeepSeek API");
                return new Failure<string>(string.Empty, ResultStatus.Error, [new ValidationFailure("AI", "HTTP request failed")]);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize DeepSeek API response");
                return new Failure<string>(string.Empty, ResultStatus.Error, [new ValidationFailure("AI", "Failed to deserialize response")]);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error when calling DeepSeek API");
                return new Failure<string>(string.Empty, ResultStatus.Error, [new ValidationFailure("AI", "Unexpected error")]);
            }
            finally
            {
                _lock.Release(); // Always release lock
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
                return JsonSerializer.Deserialize<T>(
                    response.Value,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to deserialize DeepSeek response into {Type}. Raw response: {Response}",
                    typeof(T).Name,
                    response.Value);
                return default;
            }
        }

        public string GetModelName()
        {
            return _config.Model;
        }

        #region DeepSeek API Models

        private class DeepSeekChatRequest
        {
            [JsonPropertyName("model")]
            public required string Model { get; set; }

            [JsonPropertyName("messages")]
            public required DeepSeekMessage[] Messages { get; set; }

            [JsonPropertyName("temperature")]
            public double? Temperature { get; set; }

            [JsonPropertyName("max_tokens")]
            public int? MaxTokens { get; set; }

            [JsonPropertyName("stream")]
            public bool Stream { get; set; }

            [JsonPropertyName("frequency_penalty")]
            public double FrequencyPenalty { get; set; }

            [JsonPropertyName("presence_penalty")]
            public double PresencePenalty { get; set; }

            [JsonPropertyName("top_p")]
            public double TopP { get; set; }
        }

        private class DeepSeekMessage
        {
            [JsonPropertyName("role")]
            public required string Role { get; set; }

            [JsonPropertyName("content")]
            public required string Content { get; set; }
        }

        private class DeepSeekChatResponse
        {
            [JsonPropertyName("id")]
            public string? Id { get; set; }

            [JsonPropertyName("object")]
            public string? Object { get; set; }

            [JsonPropertyName("created")]
            public long Created { get; set; }

            [JsonPropertyName("model")]
            public string? Model { get; set; }

            [JsonPropertyName("choices")]
            public DeepSeekChoice[]? Choices { get; set; }

            [JsonPropertyName("usage")]
            public DeepSeekUsage? Usage { get; set; }
        }

        private class DeepSeekChoice
        {
            [JsonPropertyName("index")]
            public int Index { get; set; }

            [JsonPropertyName("message")]
            public DeepSeekMessage? Message { get; set; }

            [JsonPropertyName("finish_reason")]
            public string? FinishReason { get; set; }
        }

        private class DeepSeekUsage
        {
            [JsonPropertyName("prompt_tokens")]
            public int PromptTokens { get; set; }

            [JsonPropertyName("completion_tokens")]
            public int CompletionTokens { get; set; }

            [JsonPropertyName("total_tokens")]
            public int TotalTokens { get; set; }
        }

        #endregion
    }

    /// <summary>
    /// Configuration for DeepSeek AI client
    /// </summary>
    public class DeepSeekClientConfig
    {
        /// <summary>
        /// Full URL for DeepSeek API endpoint (https://api.deepseek.com/chat/completions)
        /// </summary>
        public required string BaseUrl { get; set; }

        /// <summary>
        /// API Key for authentication (Bearer token)
        /// </summary>
        public required string ApiKey { get; set; }

        /// <summary>
        /// Model to use (e.g., deepseek-chat, deepseek-coder)
        /// </summary>
        public required string Model { get; set; }

        /// <summary>
        /// Temperature for response generation (0.0 - 2.0). Default: 1.0
        /// Lower = more deterministic, Higher = more creative
        /// </summary>
        public double Temperature { get; set; } = 1.0;

        /// <summary>
        /// Maximum tokens to generate in response. Default: 4096
        /// </summary>
        public int MaxTokens { get; set; } = 4096;
    }
}
