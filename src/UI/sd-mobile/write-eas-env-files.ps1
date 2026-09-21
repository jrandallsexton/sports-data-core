# Writes .env.production.tmp and .env.preview.tmp from the eas.json build
# profile env blocks, for `eas env:push`. Run from src/UI/sd-mobile:
#
#   .\write-eas-env-files.ps1
#   eas env:push --environment production --path ./.env.production.tmp
#   eas env:push --environment preview    --path ./.env.preview.tmp
#   Remove-Item .env.production.tmp, .env.preview.tmp
#
# Why a script: eas update runs the bundler locally, where Metro reads
# .env.local and inlines http://localhost:5262 into the OTA bundle. Passing
# --environment makes eas update use server-side env instead, so the server
# must hold the same EXPO_PUBLIC_* values eas build gets from eas.json.
# See docs/mobile/ota-pipeline-hardening.md, section 3.1.
#
# Never push .env.local. The *.tmp files are gitignored.

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$eas = Get-Content -Raw (Join-Path $root 'eas.json') | ConvertFrom-Json

foreach ($profile in @('production', 'preview')) {
    $env = $eas.build.$profile.env
    if (-not $env) { throw "eas.json build.$profile has no env block" }

    $lines = foreach ($p in $env.PSObject.Properties) { "$($p.Name)=$($p.Value)" }
    $content = ($lines -join "`n") + "`n"

    $path = Join-Path $root ".env.$profile.tmp"
    # LF only, no BOM. dotenv parsers tolerate CRLF, but keep it clean.
    [System.IO.File]::WriteAllText($path, $content, [System.Text.UTF8Encoding]::new($false))
    Write-Host "$path ($($lines.Count) keys)"
}
