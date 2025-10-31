// src/api/teamCardApi.js
import apiClient from "./apiClient";

const ContestApi = {
  getContestOverview: (contestId) =>
    apiClient.get(`/ui/contest/${contestId}/overview`),
  refresh: (contestId) =>
    apiClient.post(`/ui/contest/${contestId}/refresh`),
  refreshMedia: (contestId) =>
    apiClient.post(`/ui/contest/${contestId}/media/refresh`)
};

export default ContestApi;
