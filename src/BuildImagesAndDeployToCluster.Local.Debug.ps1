Write-Host "Building Api"
docker build -f "C:\Projects\sports-data\src\SportsData.Api\Dockerfile" --force-rm -t sportsdataapi  --build-arg "BUILD_CONFIGURATION=Debug" --label "com.microsoft.created-by=visual-studio" --label "com.microsoft.visual-studio.project-name=SportsData.Api" "C:\Projects\sports-data"

Write-Host ""
Write-Host "Building Contest"
docker build -f "C:\Projects\sports-data\src\SportsData.Contest\Dockerfile" --force-rm -t sportsdatacontest  --build-arg "BUILD_CONFIGURATION=Debug" --label "com.microsoft.created-by=visual-studio" --label "com.microsoft.visual-studio.project-name=SportsData.Contest" "C:\Projects\sports-data"

Write-Host ""
Write-Host "Building Franchise"
docker build -f "C:\Projects\sports-data\src\SportsData.Franchise\Dockerfile" --force-rm -t sportsdatafranchise  --build-arg "BUILD_CONFIGURATION=Debug" --label "com.microsoft.created-by=visual-studio" --label "com.microsoft.visual-studio.project-name=SportsData.Franchise" "C:\Projects\sports-data"

Write-Host ""
Write-Host "Building Notification"
docker build -f "C:\Projects\sports-data\src\SportsData.Notification\Dockerfile" --force-rm -t sportsdatanotification  --build-arg "BUILD_CONFIGURATION=Debug" --label "com.microsoft.created-by=visual-studio" --label "com.microsoft.visual-studio.project-name=SportsData.Notification" "C:\Projects\sports-data"

Write-Host ""
Write-Host "Building Player"
docker build -f "C:\Projects\sports-data\src\SportsData.Player\Dockerfile" --force-rm -t sportsdataplayer  --build-arg "BUILD_CONFIGURATION=Debug" --label "com.microsoft.created-by=visual-studio" --label "com.microsoft.visual-studio.project-name=SportsData.Player" "C:\Projects\sports-data"

Write-Host ""
Write-Host "Building Producer"
docker build -f "C:\Projects\sports-data\src\SportsData.Producer\Dockerfile" --force-rm -t sportsdataproducer  --build-arg "BUILD_CONFIGURATION=Debug" --label "com.microsoft.created-by=visual-studio" --label "com.microsoft.visual-studio.project-name=SportsData.Producer" "C:\Projects\sports-data"

Write-Host ""
Write-Host "Building Provider"
docker build -f "C:\Projects\sports-data\src\SportsData.Provider\Dockerfile" --force-rm -t sportsdataprovider  --build-arg "BUILD_CONFIGURATION=Debug" --label "com.microsoft.created-by=visual-studio" --label "com.microsoft.visual-studio.project-name=SportsData.Provider" "C:\Projects\sports-data"

Write-Host ""
Write-Host "Building Season"
docker build -f "C:\Projects\sports-data\src\SportsData.Season\Dockerfile" --force-rm -t sportsdataseason  --build-arg "BUILD_CONFIGURATION=Debug" --label "com.microsoft.created-by=visual-studio" --label "com.microsoft.visual-studio.project-name=SportsData.Season" "C:\Projects\sports-data"

Write-Host ""
Write-Host "Building Venue"
docker build -f "C:\Projects\sports-data\src\SportsData.Venue\Dockerfile" --force-rm -t sportsdatavenue  --build-arg "BUILD_CONFIGURATION=Debug" --label "com.microsoft.created-by=visual-studio" --label "com.microsoft.visual-studio.project-name=SportsData.Venue" "C:\Projects\sports-data"

# Update Kubernetes
Write-Host ""
Write-Host "Updating cluster"

# Delete Deployments
Write-Host ""
Write-Host "Deleting Deployments"
kubectl delete deployment --all --namespace=local

# Delete Services
Write-Host ""
Write-Host "Deleting Services"
kubectl delete service --all --namespace=local

# Change working directory to K8s yaml
Write-Host ""
Write-Host "Changing working directory"
Set-Location C:\Projects\sports-data-config\00_local

# Apply Deployments
Write-Host ""
Write-Host "Applying Deployments"
kubectl apply -f Deployments

# Apply Services
Write-Host ""
Write-Host "Applying Services"
kubectl apply -f Services

# Apply Prometheus Configuration
Write-Host ""
Write-Host "Applying Prometheus Configuration"
kubectl apply -f ./deployments/prometheus/prometheus-config.yml -n local

Write-Host ""
Read-Host -Prompt "Build & Deploy Complete. Press <Enter> to exit"