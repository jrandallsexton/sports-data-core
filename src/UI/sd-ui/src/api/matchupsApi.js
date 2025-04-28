// src/api/matchups.js
import apiClient from "./apiClient";

const MatchupsApi = {
  getByGroupAndWeek: (groupId, week) =>
    apiClient.get(`/matchups?groupId=${encodeURIComponent(groupId)}&week=${week}`),
};

export default MatchupsApi;
