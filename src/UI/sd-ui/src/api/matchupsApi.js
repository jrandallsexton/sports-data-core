// src/api/matchupsApi.js
import apiClient from "./apiClient";

const MatchupsApi = {
  getByLeagueAndWeek: (leagueId, weekNumber) =>
    apiClient.get(
      `/ui/leagues/${encodeURIComponent(leagueId)}/matchups/${weekNumber}`
    ),
  getPreviewByContestId: (contestId) =>
    apiClient.get(`/ui/matchup/${encodeURIComponent(contestId)}/preview`),
  // sport: backend Sport enum name (e.g. "FootballNfl"); omitted = NCAA.
  resetPreviewByContestId: (contestId, sport) =>
    apiClient.post(
      `/admin/matchup/preview/${encodeURIComponent(contestId)}/reset${sport ? `?sport=${encodeURIComponent(sport)}` : ""}`
    )
};

export default MatchupsApi;
