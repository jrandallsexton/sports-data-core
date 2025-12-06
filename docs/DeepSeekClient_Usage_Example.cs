// ========================================
// DeepSeekClient Usage Example
// ========================================
// This example shows how to configure and use DeepSeekClient
// in SportsData.Api Program.cs for local testing

using SportsData.Core.Infrastructure.Clients.AI;

// ========================================
// OPTION 1: Use DeepSeek (Hardcoded for Local Testing)
// ========================================
// Replace the Ollama AI section in Program.cs with this:

/* AI - DeepSeek (Local Testing) */
var deepSeekConfig = new DeepSeekClientConfig
{
    BaseUrl = "https://api.deepseek.com/chat/completions", // ? Correct full endpoint URL
    ApiKey = "sk-6b185de2bf7f4550bc05643b60e2f31b", // ?? Hardcoded for local testing only
    Model = "deepseek-chat", // or "deepseek-coder" for code-related tasks
    Temperature = 1.0, // 0.0 = deterministic, 2.0 = very creative
    MaxTokens = 4096   // Max tokens in response (default: 4096)
};

services.AddSingleton(deepSeekConfig);

services.AddHttpClient<DeepSeekClient>((sp, client) =>
{
    client.Timeout = TimeSpan.FromMinutes(2); // DeepSeek is usually faster than Ollama
});

services.AddSingleton<IProvideAiCommunication>(sp => sp.GetRequiredService<DeepSeekClient>());
/* End AI - DeepSeek */

// ========================================
// OPTION 2: Switch Between Ollama and DeepSeek via Config
// ========================================
// For more flexibility, use configuration to switch between providers:

/* AI - Configurable Provider */
var aiProvider = config["CommonConfig:AiProvider"] ?? "ollama"; // "ollama" or "deepseek"

if (aiProvider.Equals("deepseek", StringComparison.OrdinalIgnoreCase))
{
    // DeepSeek
    var deepSeekConfig = new DeepSeekClientConfig
    {
        BaseUrl = config["CommonConfig:DeepSeekClientConfig:BaseUrl"] 
            ?? "https://api.deepseek.com/chat/completions",
        ApiKey = config["CommonConfig:DeepSeekClientConfig:ApiKey"] 
            ?? "sk-6b185de2bf7f4550bc05643b60e2f31b",
        Model = config["CommonConfig:DeepSeekClientConfig:Model"] ?? "deepseek-chat",
        Temperature = double.TryParse(
            config["CommonConfig:DeepSeekClientConfig:Temperature"], out var temp) ? temp : 1.0,
        MaxTokens = int.TryParse(
            config["CommonConfig:DeepSeekClientConfig:MaxTokens"], out var maxTokens) ? maxTokens : 4096
    };

    services.AddSingleton(deepSeekConfig);
    services.AddHttpClient<DeepSeekClient>();
    services.AddSingleton<IProvideAiCommunication>(sp => sp.GetRequiredService<DeepSeekClient>());
}
else
{
    // Ollama (default)
    var ollamaConfig = new OllamaClientConfig
    {
        Model = config["CommonConfig:OllamaClientConfig:Model"]!,
        BaseUrl = config["CommonConfig:OllamaClientConfig:BaseUrl"]!
    };

    services.AddSingleton(ollamaConfig);
    services.AddHttpClient<OllamaClient>();
    services.AddSingleton<IProvideAiCommunication>(sp => sp.GetRequiredService<OllamaClient>());
}
/* End AI - Configurable Provider */

// ========================================
// USAGE IN CONTROLLERS/SERVICES
// ========================================
// No changes needed! IProvideAiCommunication interface works the same:

public class AdminController : ApiControllerBase
{
    private readonly IProvideAiCommunication _ai;

    public AdminController(IProvideAiCommunication ai)
    {
        _ai = ai;
    }

    [HttpPost("ai-test")]
    public async Task<IActionResult> TestAiCommunications([FromBody] AiChatCommand command)
    {
        // Works with both OllamaClient and DeepSeekClient!
        var response = await _ai.GetResponseAsync(command.Text);
        return Ok(new 
        { 
            model = _ai.GetModelName(), 
            response = response 
        });
    }

    [HttpPost("ai-test-typed")]
    public async Task<IActionResult> TestTypedResponse([FromBody] string prompt)
    {
        // Get structured response
        var analysis = await _ai.GetTypedResponseAsync<MatchupAnalysis>(prompt);
        return Ok(analysis);
    }
}

// ========================================
// EXAMPLE: Generate Matchup Preview with DeepSeek
// ========================================
public class MatchupPreviewGenerator
{
    private readonly IProvideAiCommunication _ai;

    public async Task<string> GeneratePreview(MatchupData matchup)
    {
        var prompt = $@"
Write a compelling 200-word preview for this college football matchup:

Home Team: {matchup.HomeTeam} (Record: {matchup.HomeRecord})
Home Stats: {matchup.HomeStats}

Away Team: {matchup.AwayTeam} (Record: {matchup.AwayRecord})  
Away Stats: {matchup.AwayStats}

Spread: {matchup.Spread}
Over/Under: {matchup.OverUnder}

Include:
- Key storylines
- Player matchups to watch
- Prediction with score
- One bold take

Format as HTML with <p> tags.
";

        return await _ai.GetResponseAsync(prompt);
    }
}

// ========================================
// DEEPSEEK API DETAILS (Per Official Documentation)
// ========================================
// Endpoint: https://api.deepseek.com/chat/completions
// Method: POST
// Headers:
//   - Content-Type: application/json
//   - Accept: application/json
//   - Authorization: Bearer <TOKEN>
//
// Request Body:
// {
//   "model": "deepseek-chat",
//   "messages": [
//     { "role": "user", "content": "Your prompt here" }
//   ],
//   "temperature": 1.0,
//   "max_tokens": 4096,
//   "frequency_penalty": 0,
//   "presence_penalty": 0,
//   "top_p": 1,
//   "stream": false
// }

// ========================================
// DEEPSEEK MODELS AVAILABLE
// ========================================
// - deepseek-chat: General purpose chat (recommended for most tasks)
// - deepseek-coder: Optimized for code generation and analysis
// 
// DeepSeek is faster and often produces better results than Ollama
// for sports analysis, article writing, and prediction tasks.

// ========================================
// TESTING THE CLIENT
// ========================================
// 1. Start your API: dotnet run --project src/SportsData.Api
// 2. Use the /admin/ai-test endpoint:
//
// POST http://localhost:5000/admin/ai-test
// Headers: X-Admin-Token: your-admin-token
// Body: { "text": "Explain what makes Alabama's offense so effective" }
//
// Expected response:
// {
//   "model": "deepseek-chat",
//   "response": "Alabama's offense is effective due to..."
// }

// ========================================
// PRODUCTION CONSIDERATIONS
// ========================================
// ?? DO NOT commit hardcoded API keys to source control!
// 
// For production:
// 1. Move API key to Azure App Configuration or Key Vault
// 2. Use environment variables
// 3. Implement rate limiting
// 4. Add retry policies for API failures
// 5. Monitor token usage and costs
//
// Example Azure App Configuration keys:
// CommonConfig:DeepSeekClientConfig:BaseUrl = https://api.deepseek.com/chat/completions
// CommonConfig:DeepSeekClientConfig:ApiKey = (from Key Vault)
// CommonConfig:DeepSeekClientConfig:Model = deepseek-chat
// CommonConfig:DeepSeekClientConfig:Temperature = 1.0
// CommonConfig:DeepSeekClientConfig:MaxTokens = 4096

// ========================================
// CORRECT CONFIGURATION VALUES
// ========================================
// Based on official DeepSeek API documentation:
//
// BaseUrl: "https://api.deepseek.com/chat/completions"  ? Full endpoint URL
// ApiKey: "sk-6b185de2bf7f4550bc05643b60e2f31b"          ? Your API key
// Model: "deepseek-chat"                                  ? Available models
// Temperature: 1.0 (default)                              ? 0.0-2.0 range
// MaxTokens: 4096 (default)                               ? Max response length
