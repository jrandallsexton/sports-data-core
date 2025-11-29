using SportsData.Api.Application.Admin;
using SportsData.Api.Infrastructure.Prompts;
using SportsData.Core.Infrastructure.Clients.AI;
using System.Diagnostics;

namespace SportsData.Api.Application.AI
{
    public interface IAiService
    {
        /// <summary>
        /// Simple AI chat test for debugging/testing
        /// </summary>
        Task<string> GetAiResponseAsync(string prompt, CancellationToken ct = default);

        /// <summary>
        /// Generates a game recap article from game data JSON using AI
        /// </summary>
        Task<GameRecapResponse> GenerateGameRecapAsync(GenerateGameRecapCommand command, CancellationToken ct = default);
    }

    public class AiService : IAiService
    {
        private readonly IProvideAiCommunication _ai;
        private readonly GameRecapPromptProvider _gameRecapPromptProvider;
        private readonly ILogger<AiService> _logger;

        public AiService(
            IProvideAiCommunication ai,
            GameRecapPromptProvider gameRecapPromptProvider,
            ILogger<AiService> logger)
        {
            _ai = ai;
            _gameRecapPromptProvider = gameRecapPromptProvider;
            _logger = logger;
        }

        public async Task<string> GetAiResponseAsync(string prompt, CancellationToken ct = default)
        {
            _logger.LogInformation("AI chat test request received. Prompt length: {Length} chars", prompt.Length);

            var response = await _ai.GetResponseAsync(prompt, ct);

            _logger.LogInformation(
                "AI chat test completed. Response length: {Length} chars, Model: {Model}",
                response?.Length ?? 0,
                _ai.GetModelName());

            return response ?? string.Empty;
        }

        public async Task<GameRecapResponse> GenerateGameRecapAsync(
            GenerateGameRecapCommand command,
            CancellationToken ct = default)
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
                var recap = await _ai.GetResponseAsync(fullPrompt, ct);

                sw.Stop();

                if (string.IsNullOrWhiteSpace(recap))
                {
                    _logger.LogWarning("AI returned empty response for game recap");
                    throw new InvalidOperationException("AI returned empty response");
                }

                var titleEnd = recap.LastIndexOf("*");
                var title = titleEnd > 0
                    ? recap.Substring(0, titleEnd).Trim().Trim('*', ' ', '\n', '\r')
                    : "Game Recap";

                recap = recap.Remove(0, titleEnd + 1);

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

                return response;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(
                    ex,
                    "Failed to generate game recap after {Time}ms",
                    sw.ElapsedMilliseconds);
                throw;
            }
        }
    }
}
