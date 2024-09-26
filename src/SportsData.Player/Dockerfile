#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY [".", "./src/SportsData.Player/"]
RUN dotnet nuget add source https://pkgs.dev.azure.com/jrandallsexton/sport-deets/_packaging/sports-data-nuget/nuget/v3/index.json
RUN dotnet restore "./src/SportsData.Player/SportsData.Player.csproj"
COPY . .
WORKDIR "/src/src/SportsData.Player"
RUN dotnet build "./SportsData.Player.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "./SportsData.Player.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 8080
EXPOSE 8081
ENTRYPOINT ["dotnet", "SportsData.Player.dll"]