# PowerShell wrapper for the marks batch pipeline.
#
# Dot-sources the canonical secrets file (path read from
# $env:SPORTDEETS_SECRETS_PATH so the local filesystem location stays out of
# the repo), maps the variables defined there onto the env vars the Node
# scripts read, then invokes the requested phase.
#
# Prerequisite: set SPORTDEETS_SECRETS_PATH in your user environment. Either
# point it at the .ps1 file directly, or at the directory that contains
# _common-variables.ps1 — the script handles both.
# Example (PowerShell):
#   [Environment]::SetEnvironmentVariable(
#     'SPORTDEETS_SECRETS_PATH',
#     'C:\path\to\_common-variables.ps1',  # or 'C:\path\to\secrets-dir'
#     'User')
#
# Usage:
#   ./run.ps1 -Environment dev -Phase upload
#   ./run.ps1 -Environment dev -Phase insert
#   ./run.ps1 -Environment dev -Phase insert -Manifest .\output\manifests\manifest-2026-06-05....json
#
# Environment toggles BOTH the Azure Blob account (dev vs prod) AND the
# Postgres host the inserts run against (local copy of prod vs actual prod).
# The two move together because writing marks blobs to one environment and
# pointing the DB rows at the other would create cross-env URLs.

param(
  [Parameter(Mandatory=$true)]
  [ValidateSet('upload','insert')]
  [string]$Phase,

  [ValidateSet('dev','prod')]
  [string]$Environment = 'dev',

  [ValidateSet('Mlb','Ncaafb','Nfl')]
  [string]$Sport = 'Mlb',

  # 'Teams' (default) runs upload.js / insert.js against franchise-colors data.
  # 'Athletes' runs upload-athletes.js / insert-athletes.js against
  # athletes-{sport} data and writes AthleteImage.
  [ValidateSet('Teams','Athletes')]
  [string]$Target = 'Teams',

  # Teams grain. 'Franchise' (default, go-forward) writes one year-invariant mark
  # per franchise into FranchiseLogo. 'FranchiseSeason' is the legacy per-season
  # pass (FranchiseSeasonLogo). Ignored for -Target Athletes.
  [ValidateSet('Franchise','FranchiseSeason')]
  [string]$Grain = 'Franchise',

  [string]$Scope,

  [string]$Manifest
)

$ErrorActionPreference = 'Stop'

if (-not $env:SPORTDEETS_SECRETS_PATH) {
  Write-Error "SPORTDEETS_SECRETS_PATH is not set. Point it at your secrets .ps1 file (or the directory that contains _common-variables.ps1) before running this script."
  exit 1
}
$SecretsFile = $env:SPORTDEETS_SECRETS_PATH
# Accept either a direct path to the .ps1 file OR a directory that contains
# _common-variables.ps1 — different machines/people set the env var either way.
if ((Test-Path $SecretsFile) -and (Get-Item $SecretsFile).PSIsContainer) {
  $SecretsFile = Join-Path $SecretsFile '_common-variables.ps1'
}
if (-not (Test-Path $SecretsFile)) {
  Write-Error "Secrets file not found at SPORTDEETS_SECRETS_PATH (resolved to: $SecretsFile)"
  exit 1
}
# Quote the path on dot-source so any spaces / parser-sensitive chars in
# the resolved value (e.g. "Dropbox (Personal)") don't get split.
. "$SecretsFile"

# Azure Blob — dev vs prod per the -Environment flag.
if ($Environment -eq 'prod') {
  $env:AZURE_BLOB_CONNECTION_STRING = $AzureBlobStorageProd
} else {
  $env:AZURE_BLOB_CONNECTION_STRING = $AzureBlobStorageDev
}

# Postgres host — local copy vs actual prod per the -Environment flag.
if ($Environment -eq 'prod') {
  $env:PG_HOST     = $pgHostProd
  $env:PG_USER     = $pgUserProd
  $env:PG_PASSWORD = $pgPasswordProd
} else {
  $env:PG_HOST     = $pgHostLocal
  $env:PG_USER     = $pgUserLocal
  $env:PG_PASSWORD = $pgPasswordLocal
}
$env:PG_PORT = '5432'

# Sport — picks the per-sport Producer DB and per-target source data file.
# Teams reads franchise-colors-{sport}.txt; Athletes reads athletes-{sport}.txt.
switch ($Sport) {
  'Mlb' {
    $env:PG_DATABASE = 'sdProducer.BaseballMlb'
    $env:SD_SPORT    = 'MLB'
    $sportLower      = 'mlb'
  }
  'Ncaafb' {
    $env:PG_DATABASE = 'sdProducer.FootballNcaa'
    $env:SD_SPORT    = 'NCAAFB'
    $sportLower      = 'ncaafb'
  }
  'Nfl' {
    $env:PG_DATABASE = 'sdProducer.FootballNfl'
    $env:SD_SPORT    = 'NFL'
    $sportLower      = 'nfl'
  }
}

if ($Target -eq 'Teams') {
  $env:SD_DATA_FILE = "franchise-colors-$sportLower.txt"
  if ($Grain -eq 'Franchise') {
    $env:SD_KIND = 'franchise'
    if (-not $Scope) { $Scope = 'franchise' }
  } else {
    $env:SD_KIND = 'franchise-season'
    if (-not $Scope) { $Scope = 'franchise-season:2026' }
  }
} else {
  # Athletes
  $env:SD_DATA_FILE = "athletes-$sportLower.txt"
  if (-not $Scope) { $Scope = 'athletes' }
}
$env:SD_SCOPE = $Scope

$env:SD_TARGET_ENV = $Environment

Write-Host "Phase: $Phase  Environment: $Environment  Sport: $Sport  Target: $Target  Grain: $Grain" -ForegroundColor Cyan
Write-Host "PG: $env:PG_DATABASE @ $env:PG_HOST" -ForegroundColor Cyan
Write-Host "Data file: $env:SD_DATA_FILE  Kind: $env:SD_KIND  Scope: $env:SD_SCOPE" -ForegroundColor Cyan

# Script picker: target × phase → which Node script runs.
$scriptName = if ($Target -eq 'Teams') {
  if ($Phase -eq 'upload') { 'upload.js' } else { 'insert.js' }
} else {
  if ($Phase -eq 'upload') { 'upload-athletes.js' } else { 'insert-athletes.js' }
}

if ($Phase -eq 'insert' -and $Manifest) {
  # Quote $Manifest so paths with spaces aren't split into multiple args.
  node "$PSScriptRoot\$scriptName" "$Manifest"
} else {
  node "$PSScriptRoot\$scriptName"
}
