// src/api/teamCardApi.js
import apiClient from "./apiClient";

const ContestApi = {
  getContestOverview: (contestId) =>
    apiClient.get(`/ui/contest/${contestId}/overview`)
};

export default ContestApi;
