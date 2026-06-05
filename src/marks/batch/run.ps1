# PowerShell wrapper for the marks batch pipeline.
#
# Dot-sources the canonical secrets file (path read from
# $env:SPORTDEETS_SECRETS_PATH so the local filesystem location stays out of
# the repo), maps the variables defined there onto the env vars the Node
# scripts read, then invokes the requested phase.
#
# Prerequisite: set SPORTDEETS_SECRETS_PATH in your user environment to the
# absolute path of your _common-variables.ps1 (or equivalent) secrets file.
# Example (PowerShell):
#   [Environment]::SetEnvironmentVariable(
#     'SPORTDEETS_SECRETS_PATH',
#     'C:\path\to\_common-variables.ps1',
#     'User')
#
# Usage:
#   ./run.ps1 -Environment dev -Phase upload
#   ./run.ps1 -Environment dev -Phase insert
#   ./run.ps1 -Environment dev -Phase insert -Manifest .\output\manifests\manifest-2026-06-05....json
#
# Environment toggles which Azure Blob account we upload to (dev vs prod).
# Postgres is always local for this batch — no prod-DB write path here.

param(
  [Parameter(Mandatory=$true)]
  [ValidateSet('upload','insert')]
  [string]$Phase,

  [ValidateSet('dev','prod')]
  [string]$Environment = 'dev',

  [string]$Manifest
)

$ErrorActionPreference = 'Stop'

if (-not $env:SPORTDEETS_SECRETS_PATH) {
  Write-Error "SPORTDEETS_SECRETS_PATH is not set. Point it at your secrets .ps1 file before running this script."
  exit 1
}
$SecretsFile = $env:SPORTDEETS_SECRETS_PATH
if (-not (Test-Path $SecretsFile)) {
  Write-Error "Secrets file not found at SPORTDEETS_SECRETS_PATH: $SecretsFile"
  exit 1
}
. $SecretsFile

# Azure Blob — dev vs prod per the -Environment flag.
if ($Environment -eq 'prod') {
  $env:AZURE_BLOB_CONNECTION_STRING = $AzureBlobStorageProd
} else {
  $env:AZURE_BLOB_CONNECTION_STRING = $AzureBlobStorageDev
}

# Postgres — always local for this batch. We're writing into the Producer's
# MLB database (sdProducer.BaseballMlb).
$env:PG_HOST     = $pgHostLocal
$env:PG_USER     = $pgUserLocal
$env:PG_PASSWORD = $pgPasswordLocal
$env:PG_PORT     = '5432'
$env:PG_DATABASE = 'sdProducer.BaseballMlb'

$env:SD_TARGET_ENV = $Environment

Write-Host "Phase: $Phase  Environment: $Environment" -ForegroundColor Cyan
Write-Host "PG: $env:PG_DATABASE @ $env:PG_HOST" -ForegroundColor Cyan

if ($Phase -eq 'upload') {
  node "$PSScriptRoot\upload.js"
} elseif ($Phase -eq 'insert') {
  if ($Manifest) {
    node "$PSScriptRoot\insert.js" $Manifest
  } else {
    node "$PSScriptRoot\insert.js"
  }
}
