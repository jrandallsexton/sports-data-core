import apiClient from "./apiClient";

const SeasonApi = {
  getSeasonOverview: (seasonYear) =>
    apiClient.get(`/ui/season/${seasonYear}/overview`),

  // Current-or-upcoming season for a sport, with its phases. Standard endpoint
  // returning raw phase data (TypeCode + dates); the caller interprets it —
  // e.g. the off-season countdown reads the Regular Season (TypeCode 2) phase's
  // startDate. `sport`/`league` are the route segments, e.g. ("football","ncaa").
  getCurrentSeason: (sport, league) =>
    apiClient.get(`/api/${sport}/${league}/seasons/current`),
};

export default SeasonApi;
