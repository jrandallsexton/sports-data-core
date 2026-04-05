// src/api/teamCardApi.js
import apiClient from "./apiClient";

const TeamCardApi = {
  getBySlugAndSeason: (sport, league, slug, seasonYear) =>
    apiClient.get(
      `/ui/teamcard/sport/${sport}/league/${league}/team/${slug}/${seasonYear}`
    ),
  getStatistics: (sport, league, slug, seasonYear, franchiseSeasonId) =>
    apiClient.get(
      `/ui/teamcard/sport/${sport}/league/${league}/team/${slug}/${seasonYear}/statistics`,
      {
        params: { franchiseSeasonId },
      }
    ),
  getMetrics: (sport, league, slug, seasonYear, franchiseSeasonId) =>
    apiClient.get(
      `/ui/teamcard/sport/${sport}/league/${league}/team/${slug}/${seasonYear}/metrics`,
      {
        params: { franchiseSeasonId },
      }
    ),
};

export default TeamCardApi;
