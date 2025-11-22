import apiClient from './apiClient';

const AnalyticsApi = {
  getFranchiseSeasonMetrics: (seasonYear) =>
    apiClient.get(`/ui/analytics/franchise-season/${seasonYear}`),
  
  getContestsByWeek: (seasonYear, weekNumber) =>
    apiClient.get(`/ui/analytics/contests/${seasonYear}/${weekNumber}`)
};

export default AnalyticsApi;