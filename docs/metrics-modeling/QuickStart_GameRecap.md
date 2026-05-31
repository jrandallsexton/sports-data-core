# Quick Start: Game Recap Generation

## ?? 3-Minute Setup

### 1. Upload Prompt to Blob Storage (30 seconds)

```powershell
# Using Azure Storage Explorer or Azure CLI
az storage blob upload `
  --container-name prompts `
  --file sportDeets-prompt-recap.txt `
  --name game-recap-v1.txt `
  --connection-string "your-connection-string"
```

### 2. Configure DeepSeek (30 seconds)

In `Program.cs`, replace the Ollama section with:

```csharp
/* AI - DeepSeek */
var deepSeekConfig = new DeepSeekClientConfig
{
    BaseUrl = "https://api.deepseek.com/chat/completions",
    ApiKey = "<<YOUR-KEY-HERE>>",
    Model = "deepseek-chat",
    Temperature = 1.0,
    MaxTokens = 4096
};

services.AddSingleton(deepSeekConfig);
services.AddHttpClient<DeepSeekClient>();
services.AddSingleton<IProvideAiCommunication>(sp => sp.GetRequiredService<DeepSeekClient>());
```

### 3. Test It! (2 minutes)

```powershell
# Save this as test-recap.ps1
$gameJson = Get-Content "C:\Users\Randall\Documents\wku_at_lsu.json" -Raw

$body = @{
    gameDataJson = $gameJson
    reloadPrompt = $false
} | ConvertTo-Json

$response = Invoke-RestMethod `
    -Uri "http://localhost:5000/admin/ai/game-recap" `
    -Method Post `
    -Headers @{ "X-Admin-Token" = "your-token" } `
    -Body $body `
    -ContentType "application/json"

Write-Host $response.recap
```

---

## ? Expected Result

```
# LSU Edges Western Kentucky in Defensive Slugfest

BATON ROUGE, LA - In a gritty, low-scoring affair that came down to the wire,
LSU outlasted Western Kentucky 13-10 on Saturday night at Tiger Stadium...

[Full 300-400 word game recap]

Generation Time: ~8-10 seconds
Cost: ~$0.0017 (less than 0.2 cents!)
```

---

## ?? That's It!

You now have:
- ? AI-powered game recap generation
- ? Large prompt + JSON support
- ? ~$0.002 cost per recap
- ? 8-10 second generation time

---

## ?? Full Documentation

- **Complete Guide:** `docs/GameRecapGeneration_Guide.md`
- **Summary:** `docs/DeepSeek_GameRecap_Summary.md`
- **DeepSeek Setup:** `docs/DeepSeekClient_Usage_Example.cs`

---

## ?? Pro Tips

### Update Prompts Without Restarting
```json
{
  "gameDataJson": "{ ... }",
  "reloadPrompt": true  // ? Forces reload from blob storage
}
```

### Batch Process Multiple Games
```csharp
foreach (var game in games)
{
    var response = await GenerateGameRecap(game);
    await SaveToDatabase(response.recap);
}
```

### Track Costs
```csharp
var cost = (response.estimatedPromptTokens / 1_000_000.0) * 0.14;
_logger.LogInformation("Generated recap for ${Cost:F4}", cost);
```

---

## ?? Need Help?

- **Prompt not loading?** Check Azure Blob Storage connection string
- **Empty response?** Verify DeepSeek API key
- **Too slow?** Normal for first request (loads prompt), subsequent requests faster
- **Wrong format?** Update your prompt in blob storage, set `reloadPrompt: true`

---

## ?? You're Ready!

Start generating professional-quality game recaps automatically! ??
