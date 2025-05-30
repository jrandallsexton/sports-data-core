﻿using SportsData.Core.Common;
using SportsData.Provider.Infrastructure.Data.Entities;

namespace SportsData.Provider.Infrastructure.Data.Seeders
{
    public interface ISeedResourceIndexes
    {
        List<RecurringJob> Generate(Sport sport, List<int> seasons);
    }

    public class SeederBase
    {
        private readonly List<Sport> _teamSports =
        [
            Sport.FootballNcaa,
            Sport.FootballNfl,
            Sport.BaseballMlb,
            Sport.BasketballNba
        ];

        private const string EspnApiBaseUrl = "http://sports.core.api.espn.com/v2/sports";

        public List<RecurringJob> GenerateNonSeasonalResources(
            List<RecurringJob> resources,
            Sport sport,
            string espnSportName,
            string league)
        {
            /* Venues */
            resources.Add(new RecurringJob()
            {
                Id = Guid.NewGuid(),
                Provider = SourceDataProvider.Espn,
                SportId = sport,
                DocumentType = DocumentType.Venue,
                Endpoint = $"{EspnApiBaseUrl}/{espnSportName}/leagues/{league}/venues",
                EndpointMask = null,
                CreatedBy = Guid.Empty,
                IsEnabled = true,
                Ordinal = resources.Count
            });

            ///* Athletes */
            //resources.Add(new ResourceIndex()
            //{
            //    Id = Guid.NewGuid(),
            //    Provider = SourceDataProvider.Espn,
            //    SportId = sport,
            //    DocumentType = DocumentType.Athlete,
            //    Endpoint = $"{EspnApiBaseUrl}/{espnSportName}/leagues/{league}/athletes",
            //    EndpointMask = null,
            //    CreatedBy = Guid.Empty,
            //    IsSeasonSpecific = true,
            //    IsEnabled = true,
            //    Ordinal = resources.Count
            //});

            //// While I realize this method is about non-seasonal resources,
            //// this is a ResourceIndex for all seasons. Not season-specific.
            //// We still need it.
            ///* Season */
            //resources.Add(new ResourceIndex()
            //{
            //    Id = Guid.NewGuid(),
            //    Provider = SourceDataProvider.Espn,
            //    SportId = sport,
            //    DocumentType = DocumentType.Seasons,
            //    Endpoint = $"{EspnApiBaseUrl}/{espnSportName}/leagues/{league}/seasons",
            //    EndpointMask = null,
            //    CreatedBy = Guid.Empty,
            //    IsSeasonSpecific = true,
            //    IsEnabled = true,
            //    Ordinal = resources.Count
            //});

            //if (_teamSports.Contains(sport))
            //    resources.AddRange(GenerateNonSeasonalResourcesForTeamSports(resources, sport, espnSportName, league));

            return resources;
        }

        private List<RecurringJob> GenerateNonSeasonalResourcesForTeamSports(
            List<RecurringJob> resources,
            Sport sport,
            string espnSportName,
            string league)
        {
            /* Franchises */
            resources.Add(new RecurringJob()
            {
                Id = Guid.NewGuid(),
                Provider = SourceDataProvider.Espn,
                SportId = sport,
                DocumentType = DocumentType.Franchise,
                Endpoint = $"{EspnApiBaseUrl}/{espnSportName}/leagues/{league}/franchises",
                EndpointMask = null,
                CreatedBy = Guid.Empty,
                IsEnabled = true,
                Ordinal = resources.Count
            });

            /* Positions */
            resources.Add(new RecurringJob()
            {
                Id = Guid.NewGuid(),
                Provider = SourceDataProvider.Espn,
                SportId = sport,
                DocumentType = DocumentType.Position,
                Endpoint = $"{EspnApiBaseUrl}/{espnSportName}/leagues/{league}/positions",
                EndpointMask = null,
                CreatedBy = Guid.Empty,
                IsEnabled = true,
                Ordinal = resources.Count
            });

            return resources;
        }

        public List<RecurringJob> GenerateSeasonalResources(
            List<RecurringJob> resources,
            Sport sport,
            string espnSportName,
            string league,
            int seasonYear)
        {
            // TODO: This is NOT a ResourceIndex
            ///* Season */
            //resources.Add(new ResourceIndex()
            //{
            //    Id = Guid.NewGuid(),
            //    Provider = SourceDataProvider.Espn,
            //    SportId = sport,
            //    DocumentType = DocumentType.Season,
            //    Endpoint = $"{EspnApiBaseUrl}/{espnSportName}/leagues/{league}/seasons/{seasonYear}",
            //    EndpointMask = null,
            //    CreatedBy = Guid.Empty,
            //    IsSeasonSpecific = true,
            //    IsEnabled = true,
            //    SeasonYear = seasonYear,
            //    Ordinal = resources.Count
            //});

            /* Athletes By Season */
            resources.Add(new RecurringJob()
            {
                Id = Guid.NewGuid(),
                Provider = SourceDataProvider.Espn,
                SportId = sport,
                DocumentType = DocumentType.AthleteBySeason,
                Endpoint = $"{EspnApiBaseUrl}/{espnSportName}/leagues/{league}/seasons/{seasonYear}/athletes",
                EndpointMask = null,
                CreatedBy = Guid.Empty,
                IsSeasonSpecific = true,
                IsEnabled = true,
                SeasonYear = seasonYear,
                Ordinal = resources.Count
            });

            if (_teamSports.Contains(sport))
                resources.AddRange(GenerateSeasonalResourcesForTeamSports(resources, sport, espnSportName, league, seasonYear));

            return resources;
        }

        private List<RecurringJob> GenerateSeasonalResourcesForTeamSports(
            List<RecurringJob> resources,
            Sport sport,
            string espnSportName,
            string league,
            int seasonYear)
        {
            /* SeasonBySeason */
            resources.Add(new RecurringJob()
            {
                Id = Guid.NewGuid(),
                Provider = SourceDataProvider.Espn,
                SportId = sport,
                DocumentType = DocumentType.SeasonType,
                Endpoint = $"{EspnApiBaseUrl}/{espnSportName}/leagues/{league}/seasons/{seasonYear}/types",
                EndpointMask = null,
                CreatedBy = Guid.Empty,
                IsSeasonSpecific = true,
                IsEnabled = true,
                SeasonYear = seasonYear,
                Ordinal = resources.Count
            });

            /* Teams By Season */
            resources.Add(new RecurringJob()
            {
                Id = Guid.NewGuid(),
                Provider = SourceDataProvider.Espn,
                SportId = sport,
                DocumentType = DocumentType.TeamBySeason,
                Endpoint = $"{EspnApiBaseUrl}/{espnSportName}/leagues/{league}/seasons/{seasonYear}/teams",
                EndpointMask = null,
                CreatedBy = Guid.Empty,
                IsSeasonSpecific = true,
                IsEnabled = true,
                SeasonYear = seasonYear,
                Ordinal = resources.Count
            });

            /* Coaches By Season */
            resources.Add(new RecurringJob()
            {
                Id = Guid.NewGuid(),
                Provider = SourceDataProvider.Espn,
                SportId = sport,
                DocumentType = DocumentType.CoachBySeason,
                Endpoint = $"{EspnApiBaseUrl}/{espnSportName}/leagues/{league}/seasons/{seasonYear}/coaches",
                EndpointMask = null,
                CreatedBy = Guid.Empty,
                IsSeasonSpecific = true,
                IsEnabled = false,
                SeasonYear = seasonYear,
                Ordinal = resources.Count
            });

            return resources;
        }
    }
}
