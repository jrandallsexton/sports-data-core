import apiClient from "./apiClient";

const RankingsApi = {
  getSeasonRankings: (seasonYear) =>
    apiClient.get(`/ui/rankings/${seasonYear}`),
  getCurrentRankings: (seasonYear, seasonWeek) =>
    apiClient.get(`/ui/rankings/${seasonYear}/week/${seasonWeek}`),
  getCurrentPoll: (seasonYear, seasonWeek, pollName) =>
    apiClient.get(`/ui/rankings/${seasonYear}/week/${seasonWeek}/poll/${pollName}`),
};

export default RankingsApi;