using System.Text.Json.Serialization;

namespace SportsData.Api.Application.Admin.Commands.GenerateGameRecap;

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

    /// <summary>
    /// Creates an empty GameRecapResponse for use in failure scenarios
    /// <summary>
    /// Create an empty GameRecapResponse used as a failure or placeholder result.
    /// </summary>
    /// <returns>A GameRecapResponse with all string properties set to empty and numeric properties set to zero.</returns>
    public static GameRecapResponse Empty() => new()
    {
        Model = string.Empty,
        Title = string.Empty,
        Recap = string.Empty,
        PromptVersion = string.Empty,
        EstimatedPromptTokens = 0,
        GenerationTimeMs = 0
    };
}