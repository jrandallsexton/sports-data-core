// src/api/leaderboard.js
import apiClient from "./apiClient";

const LeaderboardApi = {
  /**
   * Fetches leaderboard for a given group and week.
   * @param {string} groupId - GUID of the group
   * @returns {Promise<LeaderboardUserDto[]>}
   */
  getByGroupAndWeek: (groupId) =>
    apiClient.get(
      `/ui/leaderboard/${encodeURIComponent(groupId)}`
    ),
  
  getWidgetForUser: () => apiClient.get(`/ui/leaderboard/widget`)
};

export default LeaderboardApi;
