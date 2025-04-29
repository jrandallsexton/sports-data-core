$tag = Get-Date -Format "yyyyMMddHHmm"

Write-Host "Building Api"
docker build -f "C:\Projects\sports-data\src\SportsData.Api\Dockerfile" --force-rm -t sportsdataapi --build-arg "BUILD_CONFIGURATION=Debug" --label "com.microsoft.created-by=visual-studio" --label "com.microsoft.visual-studio.project-name=SportsData.Api" "C:\Projects\sports-data"
docker tag sportsdataapi  sportdeets.azurecr.io/sportsdataapi:$tag
docker push sportdeets.azurecr.io/sportsdataapi:$tag

Write-Host ""
Write-Host "Building Contest"
docker build -f "C:\Projects\sports-data\src\SportsData.Contest\Dockerfile" --force-rm -t sportsdatacontest --build-arg "BUILD_CONFIGURATION=Debug" --label "com.microsoft.created-by=visual-studio" --label "com.microsoft.visual-studio.project-name=SportsData.Contest" "C:\Projects\sports-data"
docker tag sportsdatacontest  sportdeets.azurecr.io/sportsdatacontest:$tag
docker push sportdeets.azurecr.io/sportsdatacontest:$tag

Write-Host ""
Write-Host "Building Franchise"
docker build -f "C:\Projects\sports-data\src\SportsData.Franchise\Dockerfile" --force-rm -t sportsdatafranchise --build-arg "BUILD_CONFIGURATION=Debug" --label "com.microsoft.created-by=visual-studio" --label "com.microsoft.visual-studio.project-name=SportsData.Franchise" "C:\Projects\sports-data"
docker tag sportsdatafranchise  sportdeets.azurecr.io/sportsdatafranchise:$tag
docker push sportdeets.azurecr.io/sportsdatafranchise:$tag

Write-Host ""
Write-Host "Building Notification"
docker build -f "C:\Projects\sports-data\src\SportsData.Notification\Dockerfile" --force-rm -t sportsdatanotification --build-arg "BUILD_CONFIGURATION=Debug" --label "com.microsoft.created-by=visual-studio" --label "com.microsoft.visual-studio.project-name=SportsData.Notification" "C:\Projects\sports-data"
docker tag sportsdatanotification  sportdeets.azurecr.io/sportsdatanotification:$tag
docker push sportdeets.azurecr.io/sportsdatanotification:$tag

Write-Host ""
Write-Host "Building Player"
docker build -f "C:\Projects\sports-data\src\SportsData.Player\Dockerfile" --force-rm -t sportsdataplayer --build-arg "BUILD_CONFIGURATION=Debug" --label "com.microsoft.created-by=visual-studio" --label "com.microsoft.visual-studio.project-name=SportsData.Player" "C:\Projects\sports-data"
docker tag sportsdataplayer  sportdeets.azurecr.io/sportsdataplayer:$tag
docker push sportdeets.azurecr.io/sportsdataplayer:$tag

Write-Host ""
Write-Host "Building Producer"
docker build -f "C:\Projects\sports-data\src\SportsData.Producer\Dockerfile" --force-rm -t sportsdataproducer --build-arg "BUILD_CONFIGURATION=Debug" --label "com.microsoft.created-by=visual-studio" --label "com.microsoft.visual-studio.project-name=SportsData.Producer" "C:\Projects\sports-data"
docker tag sportsdataproducer  sportdeets.azurecr.io/sportsdataproducer:$tag
docker push sportdeets.azurecr.io/sportsdataproducer:$tag

Write-Host ""
Write-Host "Building Provider"
docker build -f "C:\Projects\sports-data\src\SportsData.Provider\Dockerfile" --force-rm -t sportsdataprovider --build-arg "BUILD_CONFIGURATION=Debug" --label "com.microsoft.created-by=visual-studio" --label "com.microsoft.visual-studio.project-name=SportsData.Provider" "C:\Projects\sports-data"
docker tag sportsdataprovider  sportdeets.azurecr.io/sportsdataprovider:$tag
docker push sportdeets.azurecr.io/sportsdataprovider:$tag

Write-Host ""
Write-Host "Building Season"
docker build -f "C:\Projects\sports-data\src\SportsData.Season\Dockerfile" --force-rm -t sportsdataseason --build-arg "BUILD_CONFIGURATION=Debug" --label "com.microsoft.created-by=visual-studio" --label "com.microsoft.visual-studio.project-name=SportsData.Season" "C:\Projects\sports-data"
docker tag sportsdataseason  sportdeets.azurecr.io/sportsdataseason:$tag
docker push sportdeets.azurecr.io/sportsdataseason:$tag

Write-Host ""
Write-Host "Building Venue"
docker build -f "C:\Projects\sports-data\src\SportsData.Venue\Dockerfile" --force-rm -t sportsdatavenue --build-arg "BUILD_CONFIGURATION=Debug" --label "com.microsoft.created-by=visual-studio" --label "com.microsoft.visual-studio.project-name=SportsData.Venue" "C:\Projects\sports-data"
docker tag sportsdatavenue  sportdeets.azurecr.io/sportsdatavenue:$tag
docker push sportdeets.azurecr.io/sportsdatavenue:$tag

Write-Host ""
Read-Host -Prompt "Build & Upload to ACR Complete. Press <Enter> to exit"