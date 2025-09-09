import apiClient from "./apiClient";

const RankingsApi = {
  getCurrentRankings: (seasonYear, seasonWeek) =>  apiClient.get(`/ui/rankings/${seasonYear}/week/${seasonWeek}`)
};

export default RankingsApi;