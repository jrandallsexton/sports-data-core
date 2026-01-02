using FluentValidation.Results;

using SportsData.Api.Infrastructure.Prompts;
using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.AI;

using System.Diagnostics;

namespace SportsData.Api.Application.Admin.Commands.GenerateGameRecap;

public interface IGenerateGameRecapCommandHandler
{
    Task<Result<GameRecapResponse>> ExecuteAsync(GenerateGameRecapCommand command, CancellationToken cancellationToken = default);
}

public class GenerateGameRecapCommandHandler : IGenerateGameRecapCommandHandler
{
    private readonly IProvideAiCommunication _ai;
    private readonly GameRecapPromptProvider _gameRecapPromptProvider;
    private readonly ILogger<GenerateGameRecapCommandHandler> _logger;

    public GenerateGameRecapCommandHandler(
        IProvideAiCommunication ai,
        GameRecapPromptProvider gameRecapPromptProvider,
        ILogger<GenerateGameRecapCommandHandler> logger)
    {
        _ai = ai;
        _gameRecapPromptProvider = gameRecapPromptProvider;
        _logger = logger;
    }

    public async Task<Result<GameRecapResponse>> ExecuteAsync(
        GenerateGameRecapCommand command,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            // Load or reload prompt from blob storage
            var (promptText, promptName) = command.ReloadPrompt
                ? (await _gameRecapPromptProvider.ReloadPromptAsync(), "game-recap-v2") // TODO: Move to appConfig
                : await _gameRecapPromptProvider.GetGameRecapPromptAsync();

            _logger.LogInformation(
                "Loaded game recap prompt: {PromptName}, Length: {Length} chars",
                promptName,
                promptText.Length);

            // Combine prompt with game data JSON
            var fullPrompt = $"{promptText}\n\n```json\n{command.GameDataJson}\n```";

            _logger.LogInformation(
                "Full prompt size: {Size} chars (~{Tokens} tokens)",
                fullPrompt.Length,
                fullPrompt.Length / 4); // Rough token estimate: 1 token ≈ 4 chars

            // Call AI provider (DeepSeek, Ollama, etc.)
            var aiResponse = await _ai.GetResponseAsync(fullPrompt, cancellationToken);

            sw.Stop();

            if (!aiResponse.IsSuccess)
            {
                var errorMsg = aiResponse is Failure<string> f
                    ? string.Join(", ", f.Errors.Select(x => x.ErrorMessage))
                    : "Unknown error";

                _logger.LogError("AI request failed: {Error}", errorMsg);

                return new Failure<GameRecapResponse>(
                    null!,
                    aiResponse.Status,
                    aiResponse is Failure<string> failure ? failure.Errors : new List<ValidationFailure>());
            }

            var recap = aiResponse.Value;

            if (string.IsNullOrWhiteSpace(recap))
            {
                _logger.LogWarning("AI returned empty response for game recap");
                return new Failure<GameRecapResponse>(
                    null!,
                    ResultStatus.Error,
                    new List<ValidationFailure>
                    {
                        new ValidationFailure("AI.Response", "AI returned empty response")
                    });
            }

            var titleEnd = recap.LastIndexOf("*");
            string title;
            
            if (titleEnd == -1)
            {
                // No delimiter found - use entire recap as-is, default title
                title = "Game Recap";
                // recap remains unchanged
            }
            else if (titleEnd == 0)
            {
                // Delimiter at start - no title, remove leading delimiter
                title = "Game Recap";
                recap = recap.Substring(1).TrimStart('\n', '\r', ' ', '*');
            }
            else
            {
                // titleEnd > 0 - extract title before delimiter
                title = recap.Substring(0, titleEnd).Trim('*', ' ', '\n', '\r');
                recap = recap.Substring(titleEnd + 1).TrimStart('\n', '\r', ' ', '*');
            }

            var response = new GameRecapResponse
            {
                Model = _ai.GetModelName(),
                Title = title,
                Recap = recap,
                PromptVersion = promptName,
                EstimatedPromptTokens = fullPrompt.Length / 4,
                GenerationTimeMs = sw.ElapsedMilliseconds
            };

            _logger.LogInformation(
                "Game recap generated successfully. Model: {Model}, Time: {Time}ms, Output length: {Length} chars",
                response.Model,
                response.GenerationTimeMs,
                recap.Length);

            return new Success<GameRecapResponse>(response);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(
                ex,
                "Failed to generate game recap after {Time}ms",
                sw.ElapsedMilliseconds);
            return new Failure<GameRecapResponse>(
                null!,
                ResultStatus.Error,
                new List<ValidationFailure>
                {
                    new ValidationFailure("GameRecap.GenerationFailed", ex.Message)
                });
        }
    }
}
