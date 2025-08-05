// src/api/teamCardApi.js
import apiClient from "./apiClient";

const TeamCardApi = {
  getBySlugAndSeason: (slug, seasonYear) =>
    apiClient.get(`/api/ui/teamcard/sport/football/league/ncaa/team/${slug}/${seasonYear}`)
};

export default TeamCardApi;
