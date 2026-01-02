using System.Text.Json.Serialization;

namespace SportsData.Api.Application.Admin.Commands.GenerateGameRecap;

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
