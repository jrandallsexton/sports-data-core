# Shows how many users have picked up the latest production OTA update, per
# platform. Run a day or two after publish-ota.ps1, from PowerShell:
#
#   .\check-ota-adoption.ps1            # last 3 days
#   .\check-ota-adoption.ps1 -Days 7
#
# Runbook: docs/mobile/ota-updates.md.

param([int]$Days = 3)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

function Invoke-EasJson([string[]]$easArgs) {
    $ErrorActionPreference = 'Continue'
    $out = & eas @easArgs 2>$null
    if ($LASTEXITCODE -ne 0) { throw "eas $($easArgs -join ' ') exited with code $LASTEXITCODE" }
    return ($out -join "`n")
}

# One group per platform: the iOS and Android runtimes differ.
$groups = (Invoke-EasJson @('update:list', '--branch', 'production', '--limit', '2', '--json', '--non-interactive') | ConvertFrom-Json).currentPage

foreach ($g in $groups) {
    $view = Invoke-EasJson @('update:view', $g.group, '--insights', '--days', "$Days", '--json') | ConvertFrom-Json
    $u = $view.updates[0]
    $t = ($view.insights.platforms | Where-Object { $_.platform -eq $u.platform }).totals

    Write-Host "`n$($u.platform)  '$($u.message)'  published $($u.createdAt)" -ForegroundColor Cyan
    if ($u.isRollBackToEmbedded) { Write-Host '   This group is a rollback, not an update.' -ForegroundColor Yellow; continue }
    Write-Host "   unique users   $($t.uniqueUsers)"
    Write-Host "   installs       $($t.installs)"
    Write-Host "   failed         $($t.failedInstalls)"
    Write-Host "   crash rate     $($t.crashRatePercent)%"

    if ($t.failedInstalls -gt 0 -or $t.crashRatePercent -gt 0) {
        Write-Host "   BAD: failures or crashes. Roll back: eas update:rollback $($g.group)" -ForegroundColor Red
    }
    elseif ($t.uniqueUsers -le 1) {
        Write-Host '   Only you so far. Fine on day one; if still 1 after two days, users are not receiving it.' -ForegroundColor Yellow
    }
    else {
        Write-Host '   Reaching users.' -ForegroundColor Green
    }
}
