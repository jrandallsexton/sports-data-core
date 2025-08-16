using Hangfire;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Common.Routing;
using SportsData.Provider.Infrastructure.Data.Entities;

namespace SportsData.Provider.Infrastructure.Data.Seeders
{
    public interface ISeedResourceIndexes
    {
        List<ResourceIndex> Generate(Sport sport, List<int> seasons);
    }

    public class SeederBase
    {
        private readonly IGenerateRoutingKeys _routingKeyGenerator = new RoutingKeyGenerator();
        private readonly IGenerateExternalRefIdentities _identityGenerator = new ExternalRefIdentityGenerator();

        private readonly List<Sport> _teamSports =
        [
            Sport.FootballNcaa,
            Sport.FootballNfl,
            Sport.BaseballMlb,
            Sport.BasketballNba
        ];

        private const string EspnApiBaseUrl = "https://sports.core.api.espn.com/v2/sports";

        public List<ResourceIndex> GenerateNonSeasonalResources(
            List<ResourceIndex> resources,
            Sport sport,
            string espnSportName,
            string league)
        {
            /* Venues */
            resources.Add(GenerateResourceIndex(
                resources: resources,
                endpoint: $"{EspnApiBaseUrl}/{espnSportName}/leagues/{league}/venues",
                isEnabled: true,
                isRecurring: true,
                seasonYear: null,
                cronExpression: Cron.Weekly(DayOfWeek.Sunday),
                provider: SourceDataProvider.Espn,
                sport: sport,
                documentType: DocumentType.Venue));

            if (_teamSports.Contains(sport))
                GenerateNonSeasonalResourcesForTeamSports(resources, sport, espnSportName, league);

            /* Season */
            // each link in this resource index is a season definition (start and end dates, etc.)
            resources.Add(GenerateResourceIndex(
                resources: resources,
                endpoint: $"{EspnApiBaseUrl}/{espnSportName}/leagues/{league}/seasons",
                isEnabled: true,
                isRecurring: true,
                seasonYear: null,
                cronExpression: Cron.Weekly(DayOfWeek.Sunday),
                provider: SourceDataProvider.Espn,
                sport: sport,
                documentType: DocumentType.Seasons));

            return resources;
        }

        private void GenerateNonSeasonalResourcesForTeamSports(
            List<ResourceIndex> resources,
            Sport sport,
            string espnSportName,
            string league)
        {
            /* Franchises */
            //resources.Add(GenerateResourceIndex(
            //    resources: resources,
            //    endpoint: $"{EspnApiBaseUrl}/{espnSportName}/leagues/{league}/franchises",
            //    isEnabled: true,
            //    isRecurring: false,
            //    seasonYear: null,
            //    cronExpression: null,
            //    provider: SourceDataProvider.Espn,
            //    sport: sport,
            //    documentType: DocumentType.Franchise));
            resources.Add(GenerateResourceIndex(
                resources: resources,
                endpoint: $"{EspnApiBaseUrl}/{espnSportName}/leagues/{league}/franchises",
                isEnabled: true,
                isRecurring: true,
                seasonYear: null,
                cronExpression: Cron.Weekly(DayOfWeek.Sunday),
                provider: SourceDataProvider.Espn,
                sport: sport,
                documentType: DocumentType.Franchise));

            /* Positions */
            resources.Add(GenerateResourceIndex(
                resources: resources,
                endpoint: $"{EspnApiBaseUrl}/{espnSportName}/leagues/{league}/positions",
                isEnabled: true,
                isRecurring: false,
                seasonYear: null,
                cronExpression: null,
                provider: SourceDataProvider.Espn,
                sport: sport,
                documentType: DocumentType.Position));

            /* Athlete catalog */
            resources.Add(GenerateResourceIndex(
                resources: resources,
                endpoint: $"{EspnApiBaseUrl}/{espnSportName}/leagues/{league}/athletes",
                isEnabled: true,
                isRecurring: false,
                seasonYear: null,
                cronExpression: null,
                provider: SourceDataProvider.Espn,
                sport: sport,
                documentType: DocumentType.Athlete));
        }

        public void GenerateSeasonalResources(
            List<ResourceIndex> resources,
            Sport sport,
            string espnSportName,
            string league,
            int seasonYear)
        {
            if (!_teamSports.Contains(sport))
            {
                /* Athletes By Season */
                resources.Add(GenerateResourceIndex(
                    resources: resources,
                    endpoint: $"{EspnApiBaseUrl}/{espnSportName}/leagues/{league}/seasons/{seasonYear}/athletes",
                    isEnabled: true,
                    isRecurring: false,
                    seasonYear: seasonYear,
                    cronExpression: null,
                    provider: SourceDataProvider.Espn,
                    sport: sport,
                    documentType: DocumentType.AthleteSeason));
            }

            if (_teamSports.Contains(sport))
                resources.AddRange(GenerateSeasonalResourcesForTeamSports(resources, sport, espnSportName, league, seasonYear));
            
        }

        private ResourceIndex CloneResourceIndex(ResourceIndex source, bool isRecurring, string? cronExpression)
        {
            return new ResourceIndex
            {
                Id = Guid.NewGuid(),
                CreatedBy = Guid.Empty,
                CreatedUtc = DateTime.UtcNow,
                CronExpression = cronExpression,
                DocumentType = source.DocumentType,
                EndpointMask = source.EndpointMask,
                IsEnabled = true,
                IsRecurring = isRecurring,
                IsSeasonSpecific = source.SeasonYear.HasValue,
                Items = [],
                LastAccessedUtc = null,
                LastCompletedUtc = null,
                LastPageIndex = null,
                ModifiedBy = null,
                ModifiedUtc = null,
                Name = source.Name,
                Ordinal = source.Ordinal + 1, // Ensure unique ordinal
                Provider = source.Provider,
                SeasonYear = source.SeasonYear,
                SportId = source.SportId,
                TotalPageCount = null,
                Uri = source.Uri,
                SourceUrlHash = source.SourceUrlHash
            };
        }

        private ResourceIndex GenerateResourceIndex(
            List<ResourceIndex> resources,
            string endpoint,
            bool isEnabled,
            bool isRecurring,
            int? seasonYear,
            string? cronExpression,
            SourceDataProvider provider,
            Sport sport,
            DocumentType documentType)
        {
            var uri = new Uri(endpoint);
            var resourceIndex = new ResourceIndex
            {
                Id = Guid.NewGuid(),
                CreatedBy = Guid.Empty,
                CreatedUtc = DateTime.UtcNow,
                CronExpression = cronExpression,
                DocumentType = documentType,
                EndpointMask = null,
                IsEnabled = isEnabled,
                IsRecurring = isRecurring,
                IsSeasonSpecific = seasonYear.HasValue,
                Items = [],
                LastAccessedUtc = null,
                LastCompletedUtc = null,
                LastPageIndex = null,
                ModifiedBy = null,
                ModifiedUtc = null,
                Name = _routingKeyGenerator.Generate(provider, uri),
                Ordinal = resources.Count,
                Provider = provider,
                SeasonYear = seasonYear,
                SportId = sport,
                TotalPageCount = null,
                Uri = uri,
                SourceUrlHash = HashProvider.GenerateHashFromUri(uri)
            };

            // If this is a recurring resource, we also create a one-time version for initial data loading
            if (isRecurring)
            {
                var oneTime = CloneResourceIndex(resourceIndex, isRecurring: false, cronExpression: null);
                oneTime.Ordinal = resourceIndex.Ordinal + 1;
                resources.Add(oneTime);
            }

            return resourceIndex;
        }

        private List<ResourceIndex> GenerateSeasonalResourcesForTeamSports(
            List<ResourceIndex> resources,
            Sport sport,
            string espnSportName,
            string league,
            int seasonYear)
        {
            /* Awards Catalog */
            resources.Add(GenerateResourceIndex(
                resources: resources,
                endpoint: $"{EspnApiBaseUrl}/{espnSportName}/leagues/{league}/awards",
                isEnabled: true,
                isRecurring: false,
                seasonYear: null,
                cronExpression: null,
                provider: SourceDataProvider.Espn,
                sport: sport,
                documentType: DocumentType.Award));

            /* SeasonBySeason */
            resources.Add(GenerateResourceIndex(
                resources: resources,
                endpoint: $"{EspnApiBaseUrl}/{espnSportName}/leagues/{league}/seasons/{seasonYear}/types",
                isEnabled: true,
                isRecurring: false,
                seasonYear: seasonYear,
                cronExpression: null,
                provider: SourceDataProvider.Espn,
                sport: sport,
                documentType: DocumentType.SeasonType));

            /* Groups (FBS, FCS, Divisions, etc.) */
            resources.Add(GenerateResourceIndex(
                resources: resources,
                endpoint: $"{EspnApiBaseUrl}/{espnSportName}/leagues/{league}/seasons/{seasonYear}/types/3/groups",
                isEnabled: true,
                isRecurring: false,
                seasonYear: seasonYear,
                cronExpression: null,
                provider: SourceDataProvider.Espn,
                sport: sport,
                documentType: DocumentType.Group));

            /* Groups By Season (Big10, SEC, etc.) */
            resources.Add(GenerateResourceIndex(
                resources: resources,
                endpoint: $"{EspnApiBaseUrl}/{espnSportName}/leagues/{league}/seasons/{seasonYear}/types/3/groups/80/children",
                isEnabled: true,
                isRecurring: false,
                seasonYear: seasonYear,
                cronExpression: null,
                provider: SourceDataProvider.Espn,
                sport: sport,
                documentType: DocumentType.GroupSeason));

            ///* Teams By Season */
            //resources.Add(GenerateResourceIndex(
            //    resources: resources,
            //    endpoint: $"{EspnApiBaseUrl}/{espnSportName}/leagues/{league}/seasons/{seasonYear}/teams",
            //    isEnabled: true,
            //    isRecurring: false,
            //    seasonYear: seasonYear,
            //    cronExpression: null,
            //    provider: SourceDataProvider.Espn,
            //    sport: sport,
            //    documentType: DocumentType.TeamSeason));

            /* Athletes By Season */
            resources.Add(GenerateResourceIndex(
                resources: resources,
                endpoint: $"{EspnApiBaseUrl}/{espnSportName}/leagues/{league}/seasons/{seasonYear}/athletes",
                isEnabled: true,
                isRecurring: false,
                seasonYear: seasonYear,
                cronExpression: null,
                provider: SourceDataProvider.Espn,
                sport: sport,
                documentType: DocumentType.AthleteSeason));

            /* Coaches By Season */
            resources.Add(GenerateResourceIndex(
                resources: resources,
                endpoint: $"{EspnApiBaseUrl}/{espnSportName}/leagues/{league}/seasons/{seasonYear}/coaches",
                isEnabled: true,
                isRecurring: false,
                seasonYear: seasonYear,
                cronExpression: null,
                provider: SourceDataProvider.Espn,
                sport: sport,
                documentType: DocumentType.CoachSeason));

            /* Standings (DISABLED: ESPN returns 500s) */
            resources.Add(GenerateResourceIndex(
                resources: resources,
                endpoint: $"{EspnApiBaseUrl}/{espnSportName}/leagues/{league}/seasons/{seasonYear}/types/3/standings",
                isEnabled: false,
                isRecurring: false,
                seasonYear: seasonYear,
                cronExpression: null,
                provider: SourceDataProvider.Espn,
                sport: sport,
                documentType: DocumentType.Standings));

            /* Ranks (Polls, Season-wide) */
            resources.Add(GenerateResourceIndex(
                resources: resources,
                endpoint: $"{EspnApiBaseUrl}/{espnSportName}/leagues/{league}/seasons/{seasonYear}/rankings",
                isEnabled: true,
                isRecurring: false,
                seasonYear: seasonYear,
                cronExpression: null,
                provider: SourceDataProvider.Espn,
                sport: sport,
                documentType: DocumentType.TeamRank));

            return resources;
        }
    }
}
