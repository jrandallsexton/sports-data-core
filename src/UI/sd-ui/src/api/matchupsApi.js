// src/api/matchups.js
import apiClient from "./apiClient";

const MatchupsApi = {
  getByLeagueAndWeek: (leagueId, weekNumber) =>
    apiClient.get(`/ui/league/${encodeURIComponent(leagueId)}/matchups/${weekNumber}`)
};

export default MatchupsApi;
