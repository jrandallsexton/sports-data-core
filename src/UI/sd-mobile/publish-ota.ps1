# Publishes an OTA update to the production channel, after running every
# pre-publish check. Run from a PowerShell console (not Git Bash):
#
#   .\publish-ota.ps1 -Message "Short description of what changed"
#   .\publish-ota.ps1 -Message "..." -DryRun     # all checks, no publish
#
# Any failed check stops the script before anything is published. Each check
# exists because skipping it has shipped a broken or unreachable update;
# see docs/mobile/ota-pipeline-hardening.md. Runbook: docs/mobile/ota-updates.md.

#Requires -Version 7

param(
    [Parameter(Mandatory = $true)]
    [string]$Message,

    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $root '..\..\..')).Path
$branch = 'production'
$environment = 'production'

function Step([string]$text) { Write-Host "`n== $text" -ForegroundColor Cyan }
function Pass([string]$text) { Write-Host "   OK   $text" -ForegroundColor Green }
function Fail([string]$text) {
    Write-Host "   FAIL $text" -ForegroundColor Red
    Write-Host "`nNothing was published." -ForegroundColor Red
    exit 1
}

# Runs eas, returns stdout lines. eas writes notices ("new version
# available") to stderr, so stderr is kept apart and shown only on failure.
function Invoke-EasJson([string[]]$easArgs) {
    $ErrorActionPreference = 'Continue'
    $all = & eas @easArgs 2>&1
    $code = $LASTEXITCODE
    $stdout = $all | Where-Object { $_ -isnot [System.Management.Automation.ErrorRecord] }
    if ($code -ne 0) {
        $all | Where-Object { $_ -is [System.Management.Automation.ErrorRecord] } |
            ForEach-Object { Write-Host "   $_" -ForegroundColor DarkGray }
        Fail "eas $($easArgs -join ' ') exited with code $code"
    }
    return ($stdout -join "`n")
}

Set-Location $root

# ---------------------------------------------------------------------------
Step '1. Shell'
# From Git Bash, eas fingerprint:generate silently computes a wrong hash.
if ($env:MSYSTEM) {
    Fail "Running under Git Bash (MSYSTEM=$($env:MSYSTEM)). Open a PowerShell console and run the script there."
}
Pass 'PowerShell'

# ---------------------------------------------------------------------------
Step '2. Code: the mobile app matches origin/main exactly'
# eas update bundles whatever is on disk, so the tree must be exactly main.
# PowerShell scripts are not part of the bundle and are ignored here.
$paths = @('src/UI/sd-mobile', ':(exclude)src/UI/sd-mobile/*.ps1')
$ErrorActionPreference = 'Continue'
git -C $repoRoot fetch origin main --quiet
if ($LASTEXITCODE -ne 0) { Fail 'git fetch origin main failed' }
git -C $repoRoot diff --quiet origin/main -- @paths
$differs = $LASTEXITCODE -ne 0
$untracked = git -C $repoRoot status --porcelain -- @paths
$ErrorActionPreference = 'Stop'
if ($differs) {
    Fail "src/UI/sd-mobile differs from origin/main. Run: git checkout main; git pull   (and commit or stash local mobile edits)"
}
if ($untracked) {
    Fail "src/UI/sd-mobile has uncommitted or untracked files:`n$($untracked -join "`n")"
}
$head = (git -C $repoRoot rev-parse --short origin/main)
Pass "src/UI/sd-mobile is identical to origin/main ($head), no local changes"

# node_modules must match the lockfile, or the bundle contains other JS.
# npm records what it installed in node_modules/.package-lock.json.
$installedPath = Join-Path $root 'node_modules\.package-lock.json'
if (-not (Test-Path $installedPath)) { Fail 'node_modules is missing. Run: npm ci' }
$lockPkgs = (Get-Content -Raw (Join-Path $root 'package-lock.json') | ConvertFrom-Json -AsHashtable).packages
$installedPkgs = (Get-Content -Raw $installedPath | ConvertFrom-Json -AsHashtable).packages
$stale = foreach ($name in $lockPkgs.Keys) {
    if ($name -eq '') { continue }
    $want = $lockPkgs[$name]
    if (-not $installedPkgs.ContainsKey($name)) {
        # Optional packages for other platforms are legitimately absent.
        if (-not $want.optional) { "$name not installed" }
    }
    elseif ($installedPkgs[$name].version -ne $want.version) {
        "$name $($installedPkgs[$name].version), lockfile wants $($want.version)"
    }
}
if ($stale) {
    Fail ("node_modules does not match package-lock.json (run: npm ci):`n   " + (@($stale | Select-Object -First 10) -join "`n   "))
}
Pass 'node_modules matches package-lock.json'

# ---------------------------------------------------------------------------
Step '3. eas-cli ignores .env.local when publishing'
# eas update --environment sets EXPO_NO_DOTENV=1 for the bundler, so
# .env.local (API base http://localhost:5262) is never read. Verified in
# eas-cli 24.4.2 and 24.7.0. If a new CLI drops that, publishing is unsafe.
$easVersion = (& eas --version 2>$null) -join ' '
$updateJs = Join-Path (npm root -g) 'eas-cli\build\commands\update\index.js'
if (-not (Test-Path $updateJs)) {
    Fail "Cannot find $updateJs to verify eas-cli behavior. Install eas-cli globally: npm install -g eas-cli"
}
if (-not (Select-String -Path $updateJs -Pattern "EXPO_NO_DOTENV: '1'" -SimpleMatch -Quiet)) {
    Fail "$easVersion no longer sets EXPO_NO_DOTENV in eas update. Do not publish; re-verify docs/mobile/ota-pipeline-hardening.md section 3.1."
}
Pass "$easVersion sets EXPO_NO_DOTENV=1 on eas update --environment"

# ---------------------------------------------------------------------------
Step "4. Server-side env matches eas.json ($environment)"
# eas update takes EXPO_PUBLIC_* from EAS servers, not eas.json.
$expected = (Get-Content -Raw (Join-Path $root 'eas.json') | ConvertFrom-Json).build.$environment.env
$serverLines = (Invoke-EasJson @('env:list', '--environment', $environment, '--format', 'short', '--include-sensitive')) -split "`n"
$server = @{}
foreach ($line in $serverLines) {
    if ($line -match '^([A-Z0-9_]+)=(.*)$') { $server[$Matches[1]] = $Matches[2].Trim() }
}
$problems = foreach ($p in $expected.PSObject.Properties) {
    if (-not $server.ContainsKey($p.Name)) { "missing on server: $($p.Name)" }
    elseif ($server[$p.Name] -ne [string]$p.Value) { "different on server: $($p.Name)" }
}
if ($problems) {
    Fail ("Server env is out of date:`n   " + ($problems -join "`n   ") +
        "`n   Fix: .\write-eas-env-files.ps1; eas env:push --environment $environment --path ./.env.$environment.tmp; Remove-Item .env.*.tmp")
}
Pass "$(@($expected.PSObject.Properties).Count) EXPO_PUBLIC_* values match eas.json"

# ---------------------------------------------------------------------------
Step '5. Fingerprint matches the latest production store build'
# An update reaches only builds with the same runtime version. A mismatch
# means the change needs a new store build; eas update would still succeed.
$fingerprints = @{}
foreach ($platform in @('ios', 'android')) {
    $local = (Invoke-EasJson @('fingerprint:generate', '--platform', $platform, '--json', '--non-interactive') | ConvertFrom-Json).hash
    $build = (Invoke-EasJson @('build:list', '--platform', $platform, '--channel', $branch, '--status', 'finished', '--limit', '1', '--json', '--non-interactive') | ConvertFrom-Json)[0]
    if (-not $build) { Fail "No finished $platform build on the $branch channel" }
    $label = "$platform $($build.appVersion) ($($build.appBuildVersion))"
    if ($local -ne $build.runtime.version) {
        Fail "$label runs $($build.runtime.version); this code fingerprints to $local. Native code changed: this needs eas build + submit, not an OTA."
    }
    $fingerprints[$platform] = $local
    Pass "$label  $local"
}

# ---------------------------------------------------------------------------
Step "6. Channel '$branch' serves branch '$branch'"
$channel = (Invoke-EasJson @('channel:view', $branch, '--json', '--non-interactive') | ConvertFrom-Json).currentPage
$branchNames = @($channel.updateBranches | ForEach-Object { $_.name })
if ($branchNames.Count -ne 1 -or $branchNames[0] -ne $branch) {
    Fail "Channel $branch points at [$($branchNames -join ', ')]. Fix: eas channel:edit $branch --branch $branch"
}
if ($channel.isPaused) { Fail "Channel $branch is paused. Fix: eas channel:resume $branch" }
Pass "channel $branch -> branch $branch, not paused"

# ---------------------------------------------------------------------------
Step '7. Bundle check: exported bundle points at production'
# Builds the bundle the way eas update will (server env, no .env.local,
# cold Metro cache) and reads the API base out of it.
$outDir = Join-Path ([System.IO.Path]::GetTempPath()) "sd-ota-verify-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
$env:EXPO_NO_DOTENV = '1'
try {
    foreach ($platform in @('ios', 'android')) {
        Write-Host "   exporting $platform (takes a minute or two)..."
        $ErrorActionPreference = 'Continue'
        $exportLog = & eas env:exec $environment "npx expo export --clear --platform $platform --output-dir `"$outDir`"" 2>&1
        $code = $LASTEXITCODE
        $ErrorActionPreference = 'Stop'
        if ($code -ne 0) {
            $exportLog | Select-Object -Last 20 | ForEach-Object { Write-Host "   $_" -ForegroundColor DarkGray }
            Fail "Export failed for $platform. Reproduce: `$env:EXPO_NO_DOTENV='1'; eas env:exec $environment `"npx expo export --clear --platform $platform --output-dir dist-verify`""
        }
        $js = Join-Path $outDir "_expo\static\js\$platform"
        if (-not (Test-Path $js)) { Fail "No bundle at $js" }
        if (Select-String -Path "$js\*" -Pattern 'localhost:5262' -SimpleMatch -List -Quiet) {
            Fail "$platform bundle contains localhost:5262. This is the 2026-09-17 outage. Do not publish."
        }
        if (-not (Select-String -Path "$js\*" -Pattern 'api.sportdeets.com' -SimpleMatch -List -Quiet)) {
            Fail "$platform bundle does not contain api.sportdeets.com"
        }
        Pass "$platform bundle: api.sportdeets.com present, localhost:5262 absent"
        Remove-Item -Recurse -Force $outDir
    }
}
finally {
    Remove-Item Env:\EXPO_NO_DOTENV -ErrorAction SilentlyContinue
    if (Test-Path $outDir) { Remove-Item -Recurse -Force $outDir }
}

# ---------------------------------------------------------------------------
$publishArgs = @('update', '--branch', $branch, '--environment', $environment, '--clear-cache', '--message', $Message, '--non-interactive')
if ($DryRun) {
    Write-Host "`nAll checks passed. Dry run: nothing published." -ForegroundColor Green
    Write-Host "Would run: eas $($publishArgs -join ' ')"
    exit 0
}

Step '8. Publish'
Write-Host "   This goes to every production install (App Store and Play) with no review."
Write-Host "   Message: $Message"
$answer = Read-Host '   Type PUBLISH to continue'
if ($answer -cne 'PUBLISH') { Write-Host "`nCancelled. Nothing was published."; exit 1 }

$ErrorActionPreference = 'Continue'
& eas @publishArgs
$code = $LASTEXITCODE
$ErrorActionPreference = 'Stop'
if ($code -ne 0) { Write-Host "`neas update exited with code $code. Check the output above." -ForegroundColor Red; exit 1 }

# ---------------------------------------------------------------------------
Step '9. Confirm what was published'
$groups = (Invoke-EasJson @('update:list', '--branch', $branch, '--limit', '2', '--json', '--non-interactive') | ConvertFrom-Json).currentPage
$published = foreach ($g in $groups) {
    $view = Invoke-EasJson @('update:view', $g.group, '--json') | ConvertFrom-Json
    # eas-cli 24.8 returns a bare array of updates; older versions wrapped it in { updates }.
    $u = if ($view.updates) { $view.updates[0] } else { @($view)[0] }
    $ok = ($u.message -eq $Message) -and ($u.runtimeVersion -eq $fingerprints[$u.platform]) -and (-not $u.isRollBackToEmbedded)
    if (-not $ok) { Fail "Latest $($u.platform) group $($g.group) is not this publish (message '$($u.message)', runtime $($u.runtimeVersion))" }
    Pass "$($u.platform)  group $($g.group)"
    $u
}
if (@($published).Count -ne 2) { Fail 'Expected one update group per platform' }

Write-Host "`nPublished." -ForegroundColor Green
Write-Host 'Next: on your phone, fully close the app and reopen it, twice.'
Write-Host '      In a day or two: .\check-ota-adoption.ps1'
Write-Host "If it is broken: eas update:rollback <group>  (once per group above)"
