using System.Text.Json.Serialization;

namespace SportsData.Api.Application.Admin;

/// <summary>
/// Command for testing game recap generation with large JSON data
/// </summary>
public class GenerateGameRecapCommand
{
    /// <summary>
    /// Complete game data JSON (from your wku_at_lsu.json example)
    /// </summary>
    [JsonPropertyName("gameData")]
    public required string GameDataJson { get; set; }

    /// <summary>
    /// Optional: Force reload of prompt from blob storage
    /// </summary>
    [JsonPropertyName("reloadPrompt")]
    public bool ReloadPrompt { get; set; } = false;
}

/// <summary>
/// Response from game recap generation
/// </summary>
public class GameRecapResponse
{
    /// <summary>
    /// The AI model used
    /// </summary>
    [JsonPropertyName("model")]
    public required string Model { get; set; }

    /// <summary>
    /// The generated game recap title
    /// </summary>
    [JsonPropertyName("title")]
    public required string Title { get; set; }

    /// <summary>
    /// The generated game recap article
    /// </summary>
    [JsonPropertyName("recap")]
    public required string Recap { get; set; }

    /// <summary>
    /// The prompt name/version used
    /// </summary>
    [JsonPropertyName("promptVersion")]
    public required string PromptVersion { get; set; }

    /// <summary>
    /// Approximate token count of the prompt (for monitoring costs)
    /// </summary>
    [JsonPropertyName("estimatedPromptTokens")]
    public int EstimatedPromptTokens { get; set; }

    /// <summary>
    /// Time taken to generate (milliseconds)
    /// </summary>
    [JsonPropertyName("generationTimeMs")]
    public long GenerationTimeMs { get; set; }
}
