// src/api/leaderboard.js
import apiClient from "./apiClient";

const LeaderboardApi = {
  getByGroupAndWeek: (groupId, week) =>
    apiClient.get(`/leaderboard?groupId=${encodeURIComponent(groupId)}&week=${week}`),
};

export default LeaderboardApi;
