using FluentValidation.Results;

using Microsoft.Extensions.Logging;

using SportsData.Core.Common;

using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.Clients.AI;

/// <summary>
/// OpenRouter client — one OpenAI-compatible endpoint that routes to every
/// provider's models. This is the AUDITION transport for the Model Consensus
/// Lab: same prompt, many models, one account. Production panel seats use
/// direct provider clients instead (no shared single point of failure).
/// See docs/features/model-consensus-lab.md.
/// </summary>
/// <remarks>
/// Unlike <see cref="DeepSeekClient"/>, the MODEL is per-instance (ctor), not
/// config: the lab's client factory constructs one instance per enabled
/// model-catalog row. No global throttle here either — the fan-out job owns
/// concurrency, and serializing all models behind one semaphore would defeat
/// the point of a parallel audition.
/// </remarks>
public class OpenRouterClient : IProvideAiCommunication, IProvideModelEvaluation
{
    private readonly HttpClient _httpClient;
    private readonly OpenRouterClientConfig _config;
    private readonly string _model;
    private readonly ILogger _logger;

    public OpenRouterClient(
        HttpClient httpClient,
        OpenRouterClientConfig config,
        string model,
        ILogger logger)
    {
        _httpClient = httpClient;
        _config = config;
        _model = model;
        _logger = logger;

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _config.ApiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        // OpenRouter attribution headers (their recommended practice; also
        // how requests show up in their dashboard).
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("HTTP-Referer", "https://www.sportdeets.com");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Title", "sportDeets Model Lab");
    }

    public async Task<Result<AiEvaluationResult>> EvaluateAsync(
        string prompt,
        CancellationToken ct = default)
    {
        var request = new ChatRequest
        {
            Model = _model,
            Messages = [new ChatMessage { Role = "user", Content = prompt }],
            // Deterministic-as-possible for pick evaluation: the lab compares
            // MODELS, and sampling noise inside one model muddies that signal.
            Temperature = _config.Temperature,
            MaxTokens = _config.MaxTokens,
            Stream = false
        };

        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                _config.BaseUrl,
                request,
                new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull },
                ct);

            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError(
                    "OpenRouter returned {StatusCode} for model {Model}: {Error}",
                    response.StatusCode, _model, errorContent);
                return new Failure<AiEvaluationResult>(
                    default!,
                    ResultStatus.Error,
                    [new ValidationFailure("AI", $"OpenRouter {response.StatusCode} for {_model}")]);
            }

            var chatResponse = await response.Content.ReadFromJsonAsync<ChatResponse>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct);

            var content = chatResponse?.Choices is { Length: > 0 }
                ? chatResponse.Choices[0].Message?.Content?.Trim()
                : null;

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogError("OpenRouter returned empty content for model {Model}", _model);
                return new Failure<AiEvaluationResult>(
                    default!,
                    ResultStatus.Error,
                    [new ValidationFailure("AI", $"Empty content from {_model}")]);
            }

            var finishReason = chatResponse!.Choices![0].FinishReason;

            _logger.LogInformation(
                "OpenRouter response. Model={Model}, PromptTokens={PromptTokens}, CompletionTokens={CompletionTokens}, LatencyMs={LatencyMs}, FinishReason={FinishReason}",
                _model,
                chatResponse.Usage?.PromptTokens,
                chatResponse.Usage?.CompletionTokens,
                stopwatch.ElapsedMilliseconds,
                finishReason);

            if (string.Equals(finishReason, "length", StringComparison.OrdinalIgnoreCase))
            {
                // Reasoning models burn thinking tokens against the same
                // ceiling (Gemini 3.1 spent 4092 of 4096 reasoning and never
                // finished the answer) — the caller decides what to do, but
                // this is a config problem, not a model problem.
                _logger.LogWarning(
                    "OpenRouter response TRUNCATED at MaxTokens={MaxTokens} for model {Model} — content is partial",
                    _config.MaxTokens, _model);
            }

            return new Success<AiEvaluationResult>(new AiEvaluationResult(
                content,
                chatResponse.Usage?.PromptTokens,
                chatResponse.Usage?.CompletionTokens,
                stopwatch.ElapsedMilliseconds,
                finishReason));
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "OpenRouter call failed for model {Model}", _model);
            return new Failure<AiEvaluationResult>(
                default!,
                ResultStatus.Error,
                [new ValidationFailure("AI", $"{ex.GetType().Name} calling {_model}")]);
        }
    }

    public async Task<Result<string>> GetResponseAsync(string prompt, CancellationToken ct = default)
    {
        var result = await EvaluateAsync(prompt, ct);
        return result.IsSuccess
            ? new Success<string>(result.Value.Content)
            : new Failure<string>(string.Empty, result.Status, ((Failure<AiEvaluationResult>)result).Errors);
    }

    public async Task<T?> GetTypedResponseAsync<T>(string prompt, CancellationToken ct = default)
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
            _logger.LogError(ex,
                "Failed to deserialize OpenRouter response into {Type}. Model: {Model}, Raw: {Response}",
                typeof(T).Name, _model, response.Value);
            return default;
        }
    }

    public string GetModelName() => _model;

    #region OpenRouter API models (OpenAI-compatible)

    private class ChatRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; set; }

        [JsonPropertyName("messages")]
        public required ChatMessage[] Messages { get; set; }

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }
    }

    private class ChatMessage
    {
        [JsonPropertyName("role")]
        public required string Role { get; set; }

        [JsonPropertyName("content")]
        public required string Content { get; set; }
    }

    private class ChatResponse
    {
        [JsonPropertyName("choices")]
        public ChatChoice[]? Choices { get; set; }

        [JsonPropertyName("usage")]
        public ChatUsage? Usage { get; set; }
    }

    private class ChatChoice
    {
        [JsonPropertyName("message")]
        public ChatMessage? Message { get; set; }

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }

    private class ChatUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int? PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int? CompletionTokens { get; set; }
    }

    #endregion
}

/// <summary>
/// Config for the OpenRouter transport. MODEL deliberately absent — it is
/// per-catalog-row, supplied to each client instance by the lab's factory.
/// AppConfig keys: CommonConfig:OpenRouterClientConfig:{ApiKey,BaseUrl}.
/// </summary>
public class OpenRouterClientConfig
{
    /// <summary>https://openrouter.ai/api/v1/chat/completions</summary>
    public required string BaseUrl { get; set; }

    public required string ApiKey { get; set; }

    /// <summary>Low temperature: the lab compares models, and sampling noise inside one model muddies the signal.</summary>
    public double Temperature { get; set; } = 0.1;

    /// <summary>
    /// Reasoning models spend their thinking tokens against this same
    /// ceiling (Gemini 3.1 Pro burned 4092 of a 4096 cap reasoning and
    /// truncated mid-answer on every run) — so the ceiling is sized for
    /// thinking + answer, not answer alone. Worst-case cost at 16k on the
    /// priciest audition model is ~$0.25/run; a truncated run costs nearly
    /// as much and returns nothing.
    /// </summary>
    public int MaxTokens { get; set; } = 16384;
}
