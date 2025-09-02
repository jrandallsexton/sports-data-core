// src/api/leaderboard.js
import apiClient from "./apiClient";

const LeaderboardApi = {
  /**
   * Fetches leaderboard for a given group and week.
   * @param {string} groupId - GUID of the group
   * @param {number} week - Week number to calculate leaderboard
   * @returns {Promise<LeaderboardUserDto[]>}
   */
  getByGroupAndWeek: (groupId, week) =>
    apiClient.get(
      `/ui/leaderboard/${encodeURIComponent(groupId)}?week=${week}`
    ),
  
  getWidgetForUser: () => apiClient.get(`/ui/leaderboard/widget`)
};

export default LeaderboardApi;
