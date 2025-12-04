# xUnit Parallelization Setup Script
# Run this from the repository root

Write-Host "?? Setting up xUnit parallel test execution..." -ForegroundColor Cyan

# 1. Create xunit.runner.json template
$xunitConfig = @'
{
  "$schema": "https://xunit.net/schema/current/xunit.runner.schema.json",
  "parallelizeAssembly": true,
  "parallelizeTestCollections": true,
  "maxParallelThreads": 0,
  "methodDisplay": "classAndMethod",
  "preEnumerateTheories": true,
  "diagnosticMessages": false,
  "internalDiagnosticMessages": false,
  "shadowCopy": false
}
'@

# 2. Find all test projects
$testProjects = Get-ChildItem -Path "test" -Recurse -Filter "*.csproj"

Write-Host "`nFound $($testProjects.Count) test projects" -ForegroundColor Yellow

foreach ($proj in $testProjects) {
    $projDir = Split-Path $proj.FullName
    $projName = Split-Path $projDir -Leaf
    $configPath = Join-Path $projDir "xunit.runner.json"
    
    Write-Host "`nProcessing: $projName" -ForegroundColor White
    
    # Create xunit.runner.json
    if (-not (Test-Path $configPath)) {
        $xunitConfig | Out-File -FilePath $configPath -Encoding UTF8 -NoNewline
        Write-Host "  ? Created xunit.runner.json" -ForegroundColor Green
    } else {
        Write-Host "  ??  xunit.runner.json already exists" -ForegroundColor Gray
    }
    
    # Update .csproj to include the file
    $csprojContent = Get-Content $proj.FullName -Raw
    if ($csprojContent -notmatch 'xunit\.runner\.json') {
        # Find the last </ItemGroup> before </Project>
        $insertion = @'

  <ItemGroup>
    <None Update="xunit.runner.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
'@
        $csprojContent = $csprojContent -replace '(</ItemGroup>)(?![\s\S]*</ItemGroup>)', "`$1$insertion"
        $csprojContent | Out-File -FilePath $proj.FullName -Encoding UTF8 -NoNewline
        Write-Host "  ? Updated .csproj" -ForegroundColor Green
    } else {
        Write-Host "  ??  .csproj already configured" -ForegroundColor Gray
    }
}

Write-Host "`n" -NoNewline
Write-Host "?? Setup complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Review the generated xunit.runner.json files"
Write-Host "  2. Run: " -NoNewline
Write-Host "Measure-Command { dotnet test --no-build --verbosity quiet }" -ForegroundColor Yellow
Write-Host "  3. Compare with baseline performance"
Write-Host ""
Write-Host "To adjust parallelization, edit maxParallelThreads in xunit.runner.json:" -ForegroundColor Cyan
Write-Host "  • 0 = unlimited (use all cores)"
Write-Host "  • 4 = limit to 4 threads"
Write-Host "  • 8 = limit to 8 threads"
Write-Host ""
