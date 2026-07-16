import apiClient from "./apiClient";

// Cross-league pick import. Routes are league-scoped on the target league.
const ImportsApi = {
  // Candidate same-type source leagues that share >=1 contest with the target.
  getSources: (leagueId) =>
    apiClient.get(
      `/ui/leagues/${encodeURIComponent(leagueId)}/picks/import/sources`
    ),

  // Dry-run plan: { toImport[], collisions[], skipped[], targetUsesConfidencePoints }.
  getPreview: (leagueId, sourceLeagueId) =>
    apiClient.post(
      `/ui/leagues/${encodeURIComponent(leagueId)}/picks/import/preview`,
      { sourceLeagueId }
    ),

  // Commit: imports exactly the selected contestIds. Returns the summary.
  execute: (leagueId, sourceLeagueId, contestIds) =>
    apiClient.post(
      `/ui/leagues/${encodeURIComponent(leagueId)}/picks/import`,
      { sourceLeagueId, contestIds }
    ),
};

export default ImportsApi;
