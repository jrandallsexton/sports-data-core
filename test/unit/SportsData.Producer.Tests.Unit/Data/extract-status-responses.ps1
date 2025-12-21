# Extract Status Responses from Postman Collection
# This script extracts the status responses from your Postman collection and organizes them sequentially

param(
    [string]$PostmanCollectionPath = "Football.Ncaa.Espn.Event.postman_collection.json",
    [string]$OutputPath = "LiveGames\ISU_KSU_401756846"
)

Write-Host "=== ESPN Status Response Extractor ===" -ForegroundColor Cyan
Write-Host ""

# Read the Postman collection
Write-Host "Reading Postman collection from: $PostmanCollectionPath" -ForegroundColor Yellow
$collection = Get-Content $PostmanCollectionPath -Raw | ConvertFrom-Json

# Create output directory
New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null
Write-Host "Output directory: $OutputPath" -ForegroundColor Yellow
Write-Host ""

# Find the "Status" request
$statusItem = $collection.item | Where-Object { $_.name -eq "Status" }

if (-not $statusItem) {
    Write-Host "ERROR: Could not find 'Status' request in collection" -ForegroundColor Red
    exit 1
}

Write-Host "Found Status request with $($statusItem.response.Count) saved responses" -ForegroundColor Green
Write-Host ""

# Extract and save each status response
$counter = 0
foreach ($response in $statusItem.response) {
    $counter++
    $paddedNumber = $counter.ToString("D2")
    
    # Get the response name and body
    $responseName = $response.name
    $responseBody = $response.body
    
    # Create filename based on response name
    $filename = "$paddedNumber`_$responseName.json"
    
    # Replace invalid filename characters
    $filename = $filename -replace '[\\/:*?"<>|]', '_'
    
    $filepath = Join-Path $OutputPath $filename
    
    # Save the response body
    $responseBody | Out-File -FilePath $filepath -Encoding UTF8
    
    # Parse the status to show what it contains
    try {
        $statusObj = $responseBody | ConvertFrom-Json
        $statusType = $statusObj.type.name
        $displayClock = $statusObj.displayClock
        $period = $statusObj.period
        
        Write-Host "[$paddedNumber] $filename" -ForegroundColor Cyan
        Write-Host "     Status: $statusType | Period: $period | Clock: $displayClock" -ForegroundColor Gray
    }
    catch {
        Write-Host "[$paddedNumber] $filename (could not parse)" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "=== Extraction Complete ===" -ForegroundColor Green
Write-Host "Extracted $counter status responses to: $OutputPath" -ForegroundColor Green
Write-Host ""

# Also extract the competition document (static)
Write-Host "Extracting Competition document..." -ForegroundColor Yellow
$competitionItem = $collection.item | Where-Object { $_.name -eq "EventCompetition" }

if ($competitionItem -and $competitionItem.response.Count -gt 0) {
    # Use the first (or last) competition response
    $competitionResponse = $competitionItem.response[0]
    $competitionBody = $competitionResponse.body
    $competitionPath = Join-Path $OutputPath "competition.json"
    $competitionBody | Out-File -FilePath $competitionPath -Encoding UTF8
    Write-Host "? Saved competition.json" -ForegroundColor Green
}

Write-Host ""

# Also extract situation documents if they exist
Write-Host "Extracting Situation documents..." -ForegroundColor Yellow
$situationItem = $collection.item | Where-Object { $_.name -eq "Situation" }

if ($situationItem -and $situationItem.response.Count -gt 0) {
    $sitCounter = 0
    foreach ($sitResponse in $situationItem.response) {
        $sitCounter++
        $sitPadded = $sitCounter.ToString("D2")
        $sitName = $sitResponse.name
        $sitFilename = "situation-$sitPadded`_$sitName.json"
        $sitFilename = $sitFilename -replace '[\\/:*?"<>|]', '_'
        $sitPath = Join-Path $OutputPath $sitFilename
        $sitResponse.body | Out-File -FilePath $sitPath -Encoding UTF8
    }
    Write-Host "? Saved $sitCounter situation documents" -ForegroundColor Green
}

Write-Host ""
Write-Host "=== Summary ===" -ForegroundColor Cyan
Write-Host "Game: Iowa State @ Kansas State (Event 401756846)" -ForegroundColor White
Write-Host "Status responses: $counter" -ForegroundColor White
Write-Host "Directory: $OutputPath" -ForegroundColor White
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Review the extracted files in: $OutputPath" -ForegroundColor White
Write-Host "2. Update GameStateManager to point to this directory" -ForegroundColor White
Write-Host "3. Remove [Skip] attribute from FootballCompetitionStreamer_LiveGameTests" -ForegroundColor White
Write-Host "4. Run: dotnet test --filter 'StreamCompleteGame'" -ForegroundColor White
Write-Host ""
