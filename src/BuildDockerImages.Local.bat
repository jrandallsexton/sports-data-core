@echo Building Api
docker build -f "C:\Projects\sports-data\src\SportsData.Api\Dockerfile" --force-rm -t sportsdataapi  --build-arg "BUILD_CONFIGURATION=Debug" --label "com.microsoft.created-by=visual-studio" --label "com.microsoft.visual-studio.project-name=SportsData.Api" "C:\Projects\sports-data\src"

@echo Building Contest
docker build -f "C:\Projects\sports-data\src\SportsData.Contest\Dockerfile" --force-rm -t sportsdatacontest  --build-arg "BUILD_CONFIGURATION=Debug" --label "com.microsoft.created-by=visual-studio" --label "com.microsoft.visual-studio.project-name=SportsData.Contest" "C:\Projects\sports-data"

@echo Building Franchise
docker build -f "C:\Projects\sports-data\src\SportsData.Franchise\Dockerfile" --force-rm -t sportsdatafranchise  --build-arg "BUILD_CONFIGURATION=Debug" --label "com.microsoft.created-by=visual-studio" --label "com.microsoft.visual-studio.project-name=SportsData.Franchise" "C:\Projects\sports-data"

@echo Building Notification
docker build -f "C:\Projects\sports-data\src\SportsData.Notification\Dockerfile" --force-rm -t sportsdatanotification  --build-arg "BUILD_CONFIGURATION=Debug" --label "com.microsoft.created-by=visual-studio" --label "com.microsoft.visual-studio.project-name=SportsData.Notification" "C:\Projects\sports-data"

@echo Building Player
docker build -f "C:\Projects\sports-data\src\SportsData.Player\Dockerfile" --force-rm -t sportsdataplayer  --build-arg "BUILD_CONFIGURATION=Debug" --label "com.microsoft.created-by=visual-studio" --label "com.microsoft.visual-studio.project-name=SportsData.Player" "C:\Projects\sports-data"

@echo Building Producer
docker build -f "C:\Projects\sports-data\src\SportsData.Producer\Dockerfile" --force-rm -t sportsdataproducer  --build-arg "BUILD_CONFIGURATION=Debug" --label "com.microsoft.created-by=visual-studio" --label "com.microsoft.visual-studio.project-name=SportsData.Producer" "C:\Projects\sports-data"

@echo Building Provider
docker build -f "C:\Projects\sports-data\src\SportsData.Provider\Dockerfile" --force-rm -t sportsdataprovider  --build-arg "BUILD_CONFIGURATION=Debug" --label "com.microsoft.created-by=visual-studio" --label "com.microsoft.visual-studio.project-name=SportsData.Provider" "C:\Projects\sports-data"

@echo Building Season
docker build -f "C:\Projects\sports-data\src\SportsData.Season\Dockerfile" --force-rm -t sportsdataseason  --build-arg "BUILD_CONFIGURATION=Debug" --label "com.microsoft.created-by=visual-studio" --label "com.microsoft.visual-studio.project-name=SportsData.Season" "C:\Projects\sports-data"

@echo Building Venue
docker build -f "C:\Projects\sports-data\src\SportsData.Venue\Dockerfile" --force-rm -t sportsdatavenue  --build-arg "BUILD_CONFIGURATION=Debug" --label "com.microsoft.created-by=visual-studio" --label "com.microsoft.visual-studio.project-name=SportsData.Venue" "C:\Projects\sports-data"

cmd/k