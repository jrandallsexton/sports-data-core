$env:PATH = "C:\Users\Randall\AppData\Roaming\nvm\v20.19.4;" + $env:PATH
Set-Location "c:\Projects\sports-data\src\UI\sd-mobile"

Write-Host "Node version: $(node --version)"
Write-Host "Removing node_modules..."
Remove-Item -Recurse -Force node_modules -ErrorAction SilentlyContinue
Remove-Item -Force package-lock.json -ErrorAction SilentlyContinue

Write-Host "Installing..."
npm install --legacy-peer-deps --no-audit 2>&1 | Where-Object { $_ -notmatch "EBADENGINE|deprecated|WARN cleanup" }

Write-Host "Install complete. Exit: $LASTEXITCODE"
