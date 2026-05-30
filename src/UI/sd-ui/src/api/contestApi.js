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
