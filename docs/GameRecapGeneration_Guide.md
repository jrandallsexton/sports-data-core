# Game Recap Generation with DeepSeekClient

## Overview

This guide shows how to test game recap generation using the DeepSeekClient with large prompts and JSON data.

---

## Setup Steps

### 1. Upload Your Prompt to Azure Blob Storage

Upload `sportDeets-prompt-recap.txt` to Azure Blob Storage:
- **Container:** `prompts`
- **Blob Name:** `game-recap-v1.txt`

```powershell
# Using Azure Storage Explorer or Azure CLI
az storage blob upload \
  --container-name prompts \
  --file ./sportDeets-prompt-recap.txt \
  --name game-recap-v1.txt \
  --connection-string "<your-connection-string>"
```

### 2. Configure DeepSeek in Program.cs

Replace the Ollama AI configuration in `src/SportsData.Api/Program.cs` with:

```csharp
/* AI - DeepSeek */
var deepSeekConfig = new DeepSeekClientConfig
{
    BaseUrl = "https://api.deepseek.com/chat/completions",
    ApiKey = "<<YOUR-KEY-HERE>>", // Local testing only
    Model = "deepseek-chat",
    Temperature = 1.0,
    MaxTokens = 4096
};

services.AddSingleton(deepSeekConfig);
services.AddHttpClient<DeepSeekClient>();
services.AddSingleton<IProvideAiCommunication>(sp => sp.GetRequiredService<DeepSeekClient>());
/* End AI */
```

---

## Testing the Endpoint

### Endpoint Details

```
POST /admin/ai/game-recap
Headers: 
  X-Admin-Token: your-admin-token
  Content-Type: application/json
```

### Request Body

```json
{
  "gameDataJson": "{ ... your complete game JSON from wku_at_lsu.json ... }",
  "reloadPrompt": false
}
```

### Example Using HTTPie

```bash
http POST http://localhost:5000/admin/ai/game-recap \
  X-Admin-Token:your-admin-token \
  gameDataJson=@wku_at_lsu.json \
  reloadPrompt:=false
```

### Example Using cURL

```bash
curl -X POST http://localhost:5000/admin/ai/game-recap \
  -H "X-Admin-Token: your-admin-token" \
  -H "Content-Type: application/json" \
  -d '{
    "gameDataJson": "'"$(cat wku_at_lsu.json | jq -c .)"'",
    "reloadPrompt": false
  }'
```

### Example Using PowerShell

```powershell
$gameData = Get-Content -Path "wku_at_lsu.json" -Raw | ConvertFrom-Json | ConvertTo-Json -Compress

$body = @{
    gameDataJson = $gameData
    reloadPrompt = $false
} | ConvertTo-Json

Invoke-RestMethod `
    -Uri "http://localhost:5000/admin/ai/game-recap" `
    -Method Post `
    -Headers @{ "X-Admin-Token" = "your-admin-token" } `
    -Body $body `
    -ContentType "application/json"
```

---

## Response Format

```json
{
  "model": "deepseek-chat",
  "recap": "# LSU Edges Western Kentucky in Defensive Slugfest\n\nBATON ROUGE, LA - In a gritty, low-scoring affair...",
  "promptVersion": "game-recap-v1",
  "estimatedPromptTokens": 12450,
  "generationTimeMs": 8532
}
```

### Response Fields

| Field | Type | Description |
|-------|------|-------------|
| `model` | string | AI model used (e.g., "deepseek-chat") |
| `recap` | string | Generated game recap article (markdown or HTML) |
| `promptVersion` | string | Version of the prompt template used |
| `estimatedPromptTokens` | int | Approximate token count (for cost estimation) |
| `generationTimeMs` | long | Time taken to generate (milliseconds) |

---

## How It Works

### 1. Prompt Loading

The `GameRecapPromptProvider` loads your prompt from Azure Blob Storage and caches it:

```csharp
var (promptText, promptName) = await _gameRecapPromptProvider.GetGameRecapPromptAsync();
```

- First call: Loads from blob storage
- Subsequent calls: Returns cached version
- Reload: Set `reloadPrompt: true` to force refresh

### 2. Prompt + JSON Combination

Your prompt is combined with the game JSON:

```csharp
var fullPrompt = $"{promptText}\n\n```json\n{command.GameDataJson}\n```";
```

**Example Output:**
```
Role & Goal:
You are a seasoned sports journalist...

[rest of your prompt]

```json
{
  "awayMetrics": null,
  "header": {
    "awayTeam": { ... }
  }
}
```
```

### 3. DeepSeek API Call

```csharp
var recap = await _ai.GetResponseAsync(fullPrompt, CancellationToken.None);
```

- DeepSeek processes the combined prompt
- Returns a complete game recap article
- Logs token usage and timing

---

## Testing with Your Example Data

### Step 1: Save Your JSON

Save `wku_at_lsu.json` to a file:

```json
{
  "awayMetrics": null,
  "header": {
    "awayTeam": {
      "colorPrimary": "F32026",
      "displayName": "Hilltoppers",
      "finalScore": 10,
      ...
    }
  },
  ...
}
```

### Step 2: Create Test Request

```powershell
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

# Display results
Write-Host "Model: $($response.model)"
Write-Host "Generation Time: $($response.generationTimeMs)ms"
Write-Host "Estimated Tokens: $($response.estimatedPromptTokens)"
Write-Host "`nRecap:"
Write-Host $response.recap
```

### Expected Output

```
Model: deepseek-chat
Generation Time: 8532ms
Estimated Tokens: 12450

Recap:
# LSU Edges Western Kentucky in Defensive Slugfest

BATON ROUGE, LA - In a gritty, low-scoring affair that came down to the wire,
LSU outlasted Western Kentucky 13-10 on Saturday night at Tiger Stadium before
a crowd of 100,923. The game was a defensive battle from start to finish,
with both teams struggling to find offensive rhythm.

Western Kentucky drew first blood with a 36-yard field goal by J. Cannon early
in the first quarter, but LSU responded with a methodical drive culminating in
a Trey'Dez Green 11-yard touchdown reception from Michael Van Buren Jr. late
in the second quarter.

The turning point came in the fourth quarter when Western Kentucky's Dylan
Flowers returned a fumble 71 yards for a touchdown, cutting LSU's lead to just
three points with 1:05 remaining. However, the Hilltoppers' onside kick attempt
was recovered by LSU, allowing the Tigers to run out the clock.

Van Buren struggled through the air, completing 25 of 42 passes for 202 yards,
one touchdown, and one interception. LSU's defense was led by multiple players
recording interceptions, including DJ Pickett and PJ Woodland.

Despite the win, LSU's offensive performance raised concerns heading into
their next matchup. The Tigers managed just 13 points against a Hilltoppers
defense that forced two crucial turnovers.
```

---

## Prompt Size & Token Estimation

### Your Example Data

| Component | Size | Est. Tokens |
|-----------|------|-------------|
| Prompt (`game-recap-v1.txt`) | ~1,200 chars | ~300 |
| Game JSON (`wku_at_lsu.json`) | ~45,000 chars | ~11,250 |
| **Total** | **~46,200 chars** | **~11,550** |

### Token Calculation

- **Rough estimate:** 1 token ? 4 characters
- **More accurate:** Use OpenAI's tokenizer (similar for DeepSeek)
- **Your example:** ~11,550 input tokens + ~500 output tokens = **~12,050 total**

### DeepSeek Pricing (as of 2024)

- Input: $0.14 per 1M tokens
- Output: $0.28 per 1M tokens
- **Your request cost:** ~$0.0017 (less than 0.2 cents!)

---

## Troubleshooting

### Issue: "Failed to load prompt from blob storage"

**Solution:**
1. Verify blob exists: `prompts/game-recap-v1.txt`
2. Check Azure Blob Storage connection string in `appsettings.json`
3. Ensure container `prompts` exists and is accessible

### Issue: "AI returned empty response"

**Possible causes:**
1. DeepSeek API key invalid or expired
2. Request timed out (JSON too large)
3. Model reached max context length

**Solutions:**
- Verify API key is correct
- Increase HttpClient timeout in DeepSeekClient
- Reduce JSON size by removing unnecessary data

### Issue: "Request timeout"

**Solution:**
```csharp
services.AddHttpClient<DeepSeekClient>((sp, client) =>
{
    client.Timeout = TimeSpan.FromMinutes(5); // Increase timeout
});
```

---

## Advanced Usage

### Reload Prompt Without Restarting API

```http
POST /admin/ai/game-recap
{
  "gameDataJson": "{ ... }",
  "reloadPrompt": true  // ? Forces reload from blob storage
}
```

Use this when you:
- Update the prompt template in blob storage
- Want to test prompt changes without redeploying
- Need to switch between prompt versions

### Multiple Prompt Versions

Modify `GameRecapPromptProvider.cs` to support multiple versions:

```csharp
public class GameRecapPromptProvider
{
    private const string BlobNameV1 = "game-recap-v1.txt";
    private const string BlobNameV2 = "game-recap-v2-detailed.txt";
    
    public Task<(string, string)> GetGameRecapPromptAsync(int version = 1)
    {
        var blobName = version == 2 ? BlobNameV2 : BlobNameV1;
        // ...
    }
}
```

---

## Next Steps

### 1. Production Setup

- Move API key to Azure Key Vault
- Add retry policies for blob storage
- Implement response caching
- Add structured logging for monitoring

### 2. Enhancements

- Stream responses for real-time updates
- Add support for different article types (preview, recap, analysis)
- Implement batch processing for multiple games
- Add validation for generated content

### 3. Integration

- Create background job to generate recaps automatically
- Store generated articles in database
- Expose via public API endpoint
- Integrate with CMS for publishing

---

## Example: Complete Test Script

```powershell
# test-game-recap.ps1

param(
    [string]$GameJsonPath = "C:\Users\Randall\Documents\wku_at_lsu.json",
    [string]$ApiUrl = "http://localhost:5000",
    [string]$AdminToken = "your-admin-token"
)

Write-Host "Loading game JSON from $GameJsonPath..." -ForegroundColor Cyan
$gameJson = Get-Content -Path $GameJsonPath -Raw

Write-Host "Preparing request..." -ForegroundColor Cyan
$body = @{
    gameDataJson = $gameJson
    reloadPrompt = $false
} | ConvertTo-Json

Write-Host "Sending request to $ApiUrl/admin/ai/game-recap..." -ForegroundColor Cyan
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

try {
    $response = Invoke-RestMethod `
        -Uri "$ApiUrl/admin/ai/game-recap" `
        -Method Post `
        -Headers @{ "X-Admin-Token" = $AdminToken } `
        -Body $body `
        -ContentType "application/json"
    
    $stopwatch.Stop()
    
    Write-Host "`n? SUCCESS!" -ForegroundColor Green
    Write-Host "??????????????????????????????????????" -ForegroundColor Green
    Write-Host "Model:               $($response.model)"
    Write-Host "Prompt Version:      $($response.promptVersion)"
    Write-Host "Est. Input Tokens:   $($response.estimatedPromptTokens)"
    Write-Host "Generation Time:     $($response.generationTimeMs)ms"
    Write-Host "Total Time:          $($stopwatch.ElapsedMilliseconds)ms"
    Write-Host "??????????????????????????????????????" -ForegroundColor Green
    
    Write-Host "`nGenerated Recap:" -ForegroundColor Yellow
    Write-Host $response.recap
    
    # Save to file
    $outputPath = ".\game-recap-output.md"
    $response.recap | Out-File -FilePath $outputPath -Encoding UTF8
    Write-Host "`nSaved to: $outputPath" -ForegroundColor Cyan
}
catch {
    $stopwatch.Stop()
    Write-Host "`n? ERROR!" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
}
```

**Run it:**
```powershell
.\test-game-recap.ps1
```

---

## Summary

? **Created:**
- `GameRecapPromptProvider` - Loads prompts from blob storage with caching
- `GenerateGameRecapCommand` - Request model for game recap generation
- `GameRecapResponse` - Response model with metrics
- `/admin/ai/game-recap` endpoint - HTTP API for testing

? **Features:**
- Loads large prompts from Azure Blob Storage
- Combines prompts with large JSON data
- Supports prompt reloading without restart
- Tracks token usage and generation time
- Works with DeepSeek or Ollama

? **Ready to use:**
1. Upload prompt to blob storage
2. Configure DeepSeek in Program.cs
3. Send POST request with your game JSON
4. Receive generated game recap article

?? **Perfect for testing your sports journalism prompt with real game data!**
