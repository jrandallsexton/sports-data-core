# Create GitHub Issues for API Wrapper Refactoring

# Load token
. "D:\Dropbox\Code\sports-data-provision\_secrets\_common-variables.ps1"

if (-not $ghSportDeetsProjectToken) {
    Write-Error "Token not found"
    exit 1
}

$owner = "jrandallsexton"
$repo = "sports-data-core"

$headers = @{
    "Authorization" = "Bearer $ghSportDeetsProjectToken"
    "Accept" = "application/vnd.github+json"
}

$issues = @(
    "Wrap /admin/errors/competitions-without-competitors response|AdminController.cs:119|admin",
    "Wrap /admin/errors/competitions-without-plays response|AdminController.cs:125|admin",
    "Wrap /admin/errors/competitions-without-drives response|AdminController.cs:131|admin",
    "Wrap /admin/errors/competitions-without-metrics response|AdminController.cs:137|admin",
    "Wrap admin bulk delete operation request/response|AdminController.cs:147|admin",
    "Wrap /ui/rankings response (first endpoint)|RankingsController.cs:25|ui",
    "Wrap /ui/rankings response (second endpoint)|RankingsController.cs:56|ui",
    "Wrap /ui/picks response|PicksController.cs:44|ui",
    "Wrap /ui/picks/chart response|PicksController.cs:78|ui",
    "Wrap /ui/analytics response|AnalyticsController.cs:19|ui",
    "Wrap /ui/conferences response|ConferenceController.cs:17|ui",
    "Wrap /ui/leagues response (first endpoint)|LeagueController.cs:95|ui",
    "Wrap /ui/leagues/discover response|LeagueController.cs:208|ui",
    "Wrap /ui/leaderboard response|LeaderboardController.cs:22|ui"
)

Write-Host "`nCreating $($issues.Count) issues..." -ForegroundColor Cyan

foreach ($issueData in $issues) {
    $parts = $issueData -split '\|'
    $title = $parts[0]
    $location = $parts[1]
    $category = $parts[2]
    
    $body = "**Location:** $location`n`n**Current:** Returns naked collection`n**Target:** Return wrapped response object`n`n**Acceptance Criteria:**`n- [ ] Create response DTO`n- [ ] Update controller`n- [ ] Update frontend if needed`n- [ ] Update tests`n- [ ] Verify Swagger`n`nSee docs/API-WRAPPER-REFACTORING.md"
    
    $payload = @{
        title = $title
        body = $body
        labels = @("api", "refactoring", $category)
    } | ConvertTo-Json
    
    try {
        $result = Invoke-RestMethod -Uri "https://api.github.com/repos/$owner/$repo/issues" `
            -Method Post -Headers $headers -Body $payload -ContentType "application/json"
        
        Write-Host "✓ #$($result.number): $title" -ForegroundColor Green
        Start-Sleep -Seconds 1
    }
    catch {
        Write-Host "✗ Failed: $title" -ForegroundColor Red
        Write-Host "  $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "`nDone! View at: https://github.com/$owner/$repo/issues" -ForegroundColor Cyan
