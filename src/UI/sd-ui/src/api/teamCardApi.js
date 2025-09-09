// src/api/teamCardApi.js
import apiClient from "./apiClient";

const TeamCardApi = {
  getBySlugAndSeason: (slug, seasonYear) =>
    apiClient.get(`/ui/teamcard/sport/football/league/ncaa/team/${slug}/${seasonYear}`),
  getStatistics: (slug, seasonYear, franchiseSeasonId) =>
    apiClient.get(`/ui/teamcard/sport/football/league/ncaa/team/${slug}/${seasonYear}/statistics`, {
      params: { franchiseSeasonId },
    }),
};

export default TeamCardApi;
