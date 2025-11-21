# Generate-Predictions.ps1
<#
.SYNOPSIS
    Automates the complete MetricBot prediction workflow from SQL queries to DTO generation.

.DESCRIPTION
    This script executes the full prediction pipeline:
    1. Runs PostgreSQL queries to extract training and current week data
    2. Combines CSV files for full dataset
    3. Executes straight-up and ATS prediction models
    4. Generates MetricBot DTOs ready for Postman import

.PARAMETER WeekNumber
    The week number for predictions (default: auto-detect current week)

.PARAMETER PickemGroupId
    The PickemGroup ID for MetricBot (default: aa7a482f-2204-429a-bb7c-75bc2dfef92b)

.PARAMETER SkipQueries
    Skip SQL query execution and use existing CSV files

.EXAMPLE
    .\Generate-Predictions.ps1
    
.EXAMPLE
    .\Generate-Predictions.ps1 -WeekNumber 13 -PickemGroupId "your-group-id"
#>

[CmdletBinding()]
param(
    [Parameter()]
    [int]$WeekNumber = 0,  # 0 = auto-detect

    [Parameter()]
    [switch]$SkipQueries
)

# ============================================
# Configuration
# ============================================

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$dataDir = Join-Path $scriptDir "data"
$sqlDir = Join-Path $scriptDir "sql"
$venvDir = Join-Path $scriptDir ".venv"

# Ensure data directory exists
if (-not (Test-Path $dataDir)) {
    New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
}

# Load PostgreSQL connection from environment
if (-not $env:SPORTDEETS_SECRETS_PATH) {
    throw "ERROR: SPORTDEETS_SECRETS_PATH environment variable is not set."
}

. "$env:SPORTDEETS_SECRETS_PATH\_common-variables.ps1"

$pgHost = $script:pgHostProd
$pgUser = $script:pgUserProd
$pgPassword = $script:pgPasswordProd
$pgDatabase = "sdProducer.FootballNcaa"

# Validate psql is available
if (-not (Get-Command psql -ErrorAction SilentlyContinue)) {
    throw "ERROR: psql command not found. Please install PostgreSQL client tools."
}

# Validate Python virtual environment
if (-not (Test-Path $venvDir)) {
    throw "ERROR: Python virtual environment not found at $venvDir. Please create it with: python -m venv .venv"
}

$pythonExe = Join-Path $venvDir "Scripts\python.exe"
if (-not (Test-Path $pythonExe)) {
    throw "ERROR: Python executable not found at $pythonExe"
}

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "MetricBot Prediction Pipeline" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# ============================================
# Step 0: Auto-detect week number if needed
# ============================================

if ($WeekNumber -eq 0) {
    Write-Host "[Step 0] Auto-detecting current week..." -ForegroundColor Yellow
    
    $weekQuerySql = @"
SELECT sw."Number" AS "WeekNumber"
FROM public."Season" s
JOIN public."SeasonWeek" sw ON sw."SeasonId" = s."Id"
JOIN public."SeasonPhase" sp ON sp."Id" = sw."SeasonPhaseId"
WHERE sp."Name" = 'Regular Season'
  AND sw."StartDate" <= NOW()
  AND sw."EndDate" > NOW()
ORDER BY sw."StartDate"
LIMIT 1;
"@

    $env:PGPASSWORD = $pgPassword
    $weekResult = $weekQuerySql | psql -h $pgHost -U $pgUser -d $pgDatabase -t -A -F ","
    $env:PGPASSWORD = $null
    
    if ($weekResult -match '^\d+$') {
        $WeekNumber = [int]$weekResult
        Write-Host "  ✅ Detected week: $WeekNumber" -ForegroundColor Green
    } else {
        throw "ERROR: Could not auto-detect current week. Please specify -WeekNumber parameter."
    }
    Write-Host ""
}

# Update filenames with week number
$currentWeekFile = "competition_metrics_week_$WeekNumber.csv"
$currentWeekPath = Join-Path $dataDir $currentWeekFile

# ============================================
# Step 1: Execute SQL queries
# ============================================

if (-not $SkipQueries) {
    Write-Host "[Step 1] Executing PostgreSQL queries..." -ForegroundColor Yellow
    
    # Training data query
    Write-Host "  - Extracting training data (completed games)..." -ForegroundColor Gray
    $trainingSql = Join-Path $sqlDir "competition_metrics_training.sql"
    $trainingOutput = Join-Path $dataDir "competition_metrics.csv"
    
    $env:PGPASSWORD = $pgPassword
    psql -h $pgHost -U $pgUser -d $pgDatabase -f $trainingSql -o $trainingOutput --csv
    $env:PGPASSWORD = $null
    
    if (-not (Test-Path $trainingOutput)) {
        throw "ERROR: Training data CSV not created at $trainingOutput"
    }
    
    $trainingRows = (Get-Content $trainingOutput | Measure-Object -Line).Lines - 1
    Write-Host "    ✅ Training data: $trainingRows rows" -ForegroundColor Green
    
    # Current week query
    Write-Host "  - Extracting current week data (week $WeekNumber)..." -ForegroundColor Gray
    $currentWeekSql = Join-Path $sqlDir "competition_metrics_current_week.sql"
    
    $env:PGPASSWORD = $pgPassword
    psql -h $pgHost -U $pgUser -d $pgDatabase -f $currentWeekSql -o $currentWeekPath --csv
    $env:PGPASSWORD = $null
    
    if (-not (Test-Path $currentWeekPath)) {
        throw "ERROR: Current week CSV not created at $currentWeekPath"
    }
    
    $currentWeekRows = (Get-Content $currentWeekPath | Measure-Object -Line).Lines - 1
    Write-Host "    ✅ Current week data: $currentWeekRows rows" -ForegroundColor Green
    Write-Host ""
} else {
    Write-Host "[Step 1] Skipping SQL queries (using existing CSV files)" -ForegroundColor Yellow
    Write-Host ""
}

# ============================================
# Step 2: Combine CSV files
# ============================================

Write-Host "[Step 2] Combining CSV files..." -ForegroundColor Yellow

# Update combine_csv.py with current week number
$combineScript = Join-Path $scriptDir "combine_csv.py"
$combineContent = Get-Content $combineScript -Raw

# Replace week number in the script (update line that reads week CSV)
$combineContent = $combineContent -replace 'competition_metrics_week_\d+\.csv', $currentWeekFile

Set-Content -Path $combineScript -Value $combineContent

# Execute Python script
& $pythonExe $combineScript

if ($LASTEXITCODE -ne 0) {
    throw "ERROR: combine_csv.py failed with exit code $LASTEXITCODE"
}

$fullCsvPath = Join-Path $dataDir "competition_metrics_full.csv"
if (-not (Test-Path $fullCsvPath)) {
    throw "ERROR: Full dataset CSV not created at $fullCsvPath"
}

$fullRows = (Get-Content $fullCsvPath | Measure-Object -Line).Lines - 1
Write-Host "  ✅ Full dataset: $fullRows rows" -ForegroundColor Green
Write-Host ""

# ============================================
# Step 3: Generate straight-up predictions
# ============================================

Write-Host "[Step 3] Generating straight-up predictions..." -ForegroundColor Yellow

$straightupScript = Join-Path $scriptDir "predict_straightup.py"

# Update script to use current week file
$straightupContent = Get-Content $straightupScript -Raw
$straightupContent = $straightupContent -replace 'competition_metrics_week_\d+\.csv', $currentWeekFile
Set-Content -Path $straightupScript -Value $straightupContent

& $pythonExe $straightupScript

if ($LASTEXITCODE -ne 0) {
    throw "ERROR: predict_straightup.py failed with exit code $LASTEXITCODE"
}

$straightupOutputPath = Join-Path $dataDir "predictions_straightup_raw.csv"
if (-not (Test-Path $straightupOutputPath)) {
    throw "ERROR: Straight-up predictions not created at $straightupOutputPath"
}

$straightupRows = (Get-Content $straightupOutputPath | Measure-Object -Line).Lines - 1
Write-Host "  ✅ Straight-up predictions: $straightupRows contests" -ForegroundColor Green
Write-Host ""

# ============================================
# Step 4: Generate ATS predictions
# ============================================

Write-Host "[Step 4] Generating ATS predictions..." -ForegroundColor Yellow

$atsScript = Join-Path $scriptDir "predict_ats.py"

# Update script to use current week file
$atsContent = Get-Content $atsScript -Raw
$atsContent = $atsContent -replace 'competition_metrics_week_\d+\.csv', $currentWeekFile
Set-Content -Path $atsScript -Value $atsContent

& $pythonExe $atsScript

if ($LASTEXITCODE -ne 0) {
    throw "ERROR: predict_ats.py failed with exit code $LASTEXITCODE"
}

$atsOutputPath = Join-Path $dataDir "predictions_ats_raw.csv"
if (-not (Test-Path $atsOutputPath)) {
    throw "ERROR: ATS predictions not created at $atsOutputPath"
}

$atsRows = (Get-Content $atsOutputPath | Measure-Object -Line).Lines - 1
Write-Host "  ✅ ATS predictions: $atsRows contests" -ForegroundColor Green
Write-Host ""

# ============================================
# Step 5: Merge predictions and generate DTOs
# ============================================

Write-Host "[Step 5] Generating Contest Prediction DTOs..." -ForegroundColor Yellow

# Use the existing generate_contest_prediction_dtos.py script
$dtoScript = Join-Path $scriptDir "generate_contest_prediction_dtos.py"

# Update script to use current week file
$dtoContent = Get-Content $dtoScript -Raw
$dtoContent = $dtoContent -replace 'predictions_straightup_raw\.csv', "predictions_straightup_raw.csv"
$dtoContent = $dtoContent -replace 'predictions_ats_raw\.csv', "predictions_ats_raw.csv"
Set-Content -Path $dtoScript -Value $dtoContent

& $pythonExe $dtoScript

if ($LASTEXITCODE -ne 0) {
    throw "ERROR: generate_contest_prediction_dtos.py failed with exit code $LASTEXITCODE"
}

$dtosOutputPath = Join-Path $dataDir "contest_predictions.json"
if (-not (Test-Path $dtosOutputPath)) {
    throw "ERROR: DTOs JSON not created at $dtosOutputPath"
}

$dtosContent = Get-Content $dtosOutputPath -Raw | ConvertFrom-Json
$dtosCount = $dtosContent.Count

Write-Host "  ✅ Generated $dtosCount DTOs" -ForegroundColor Green
Write-Host ""

# ============================================
# Summary
# ============================================

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "PREDICTION PIPELINE COMPLETE! 🎉" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Week Number:         $WeekNumber" -ForegroundColor White
Write-Host "Training Games:      $trainingRows" -ForegroundColor White
Write-Host "Current Week Games:  $currentWeekRows" -ForegroundColor White
Write-Host "Predictions:         $straightupRows" -ForegroundColor White
Write-Host "DTOs Generated:      $dtosCount" -ForegroundColor White
Write-Host ""
Write-Host "Output Files:" -ForegroundColor Yellow
Write-Host "  - Training data:      $trainingOutput" -ForegroundColor Gray
Write-Host "  - Current week:       $currentWeekPath" -ForegroundColor Gray
Write-Host "  - Full dataset:       $fullCsvPath" -ForegroundColor Gray
Write-Host "  - Straight-up preds:  $straightupOutputPath" -ForegroundColor Gray
Write-Host "  - ATS preds:          $atsOutputPath" -ForegroundColor Gray
Write-Host "  - Contest DTOs:       $dtosOutputPath" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "  1. Review predictions in CSVs above" -ForegroundColor White
Write-Host "  2. Import DTOs to Postman from: $dtosOutputPath" -ForegroundColor White
Write-Host "  3. POST to: /api/admin/ai-predictions/b210d677-19c3-4f26-ac4b-b2cc7ad58c44" -ForegroundColor White
Write-Host "     (MetricBot synthetic user ID)" -ForegroundColor Gray
Write-Host ""
