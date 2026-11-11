import apiClient from './apiClient';

const AnalyticsApi = {
  getFranchiseSeasonMetrics: (seasonYear) =>
    apiClient.get(`/ui/analytics/franchise-season/${seasonYear}`),

  // Season leaderboard for one athlete stat; category/stat are ESPN names
  // (e.g. passing / passingYards).
  getAthleteSeasonLeaders: (seasonYear, { category, stat, perGame = false, limit = 10 }) =>
    apiClient.get(`/ui/analytics/leaders/${seasonYear}`, {
      params: { category, stat, perGame, limit },
    }),
};

export default AnalyticsApi;
