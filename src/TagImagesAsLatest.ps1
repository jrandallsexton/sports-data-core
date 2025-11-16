param(
    [string]$Tag = $(Get-Date -Format "yyyyMMddHHmm")
)

Write-Host "Tagging images with timestamp $Tag as latest..."

docker tag sportdeets.azurecr.io/sportsdataapi:$Tag sportdeets.azurecr.io/sportsdataapi:latest
docker push sportdeets.azurecr.io/sportsdataapi:latest

docker tag sportdeets.azurecr.io/sportsdataproducer:$Tag sportdeets.azurecr.io/sportsdataproducer:latest
docker push sportdeets.azurecr.io/sportsdataproducer:latest

docker tag sportdeets.azurecr.io/sportsdataprovider:$Tag sportdeets.azurecr.io/sportsdataprovider:latest
docker push sportdeets.azurecr.io/sportsdataprovider:latest

Write-Host ""
Write-Host "Tagged and pushed API, Producer, and Provider as :latest"
