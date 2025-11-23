// src/api/matchupsApi.js
import apiClient from "./apiClient";

const MatchupsApi = {
  getByLeagueAndWeek: (leagueId, weekNumber) =>
    apiClient.get(
      `/ui/leagues/${encodeURIComponent(leagueId)}/matchups/${weekNumber}`
    ),
  getPreviewByContestId: (contestId) =>
    apiClient.get(`/ui/matchup/${encodeURIComponent(contestId)}/preview`),
  resetPreviewByContestId: (contestId) =>
    apiClient.post(
      `/admin/matchup/preview/${encodeURIComponent(contestId)}/reset`
    )
};

export default MatchupsApi;
