// src/api/contestApi.js
import apiClient from "./apiClient";

const ContestApi = {
  getContestOverview: (contestId, sport, league) =>
    apiClient.get(`/ui/contest/${contestId}/overview`, {
      params: { sport, league }
    }),
  refresh: (contestId, sport, league) =>
    apiClient.post(`/ui/contest/${contestId}/refresh`, null, {
      params: { sport, league }
    }),
  refreshMedia: (contestId, sport, league) =>
    apiClient.post(`/ui/contest/${contestId}/media/refresh`, null, {
      params: { sport, league }
    })
};

export default ContestApi;
