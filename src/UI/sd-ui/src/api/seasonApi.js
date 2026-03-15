import apiClient from "./apiClient";

const SeasonApi = {
  getSeasonOverview: (seasonYear) =>
    apiClient.get(`/ui/season/${seasonYear}/overview`),
};

export default SeasonApi;
