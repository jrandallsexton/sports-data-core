// src/api/contestApi.js
import apiClient from "./apiClient";

const ContestApi = {
  getContestOverview: (contestId, sport, league) =>
    apiClient.get(`/ui/contest/${contestId}/overview`, {
      params: { sport, league }
    }),
  // On-demand full play log. The overview endpoint above returns only the
  // significant plays (scoring + priority); this fetches every play and
  // backs the "Show all plays" toggle.
  getContestPlayLog: (contestId, sport, league) =>
    apiClient.get(`/ui/contest/${contestId}/playlog`, {
      params: { sport, league }
    }),
  // Historical context for a matchup: last N head-to-head meetings and each
  // team's late-prior-season form — the same blocks the preview/insight
  // models consume. Preview-safe semantics (finalized only, preseason
  // excluded, no as-of leakage) are baked in server-side.
  getHistory: (sport, league, contestId) =>
    apiClient.get(`/api/${sport}/${league}/contests/${contestId}/history`),
  refresh: (contestId, sport, league) =>
    apiClient.post(`/ui/contest/${contestId}/refresh`, null, {
      params: { sport, league }
    }),
  refreshMedia: (contestId, sport, league) =>
    apiClient.post(`/ui/contest/${contestId}/media/refresh`, null, {
      params: { sport, league }
    }),
  finalize: (contestId, sport, league) =>
    apiClient.post(`/ui/contest/${contestId}/finalize`, null, {
      params: { sport, league }
    })
};

export default ContestApi;
