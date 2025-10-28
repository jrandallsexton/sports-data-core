import apiClient from './apiClient';

const AnalyticsApi = {
  getFranchiseSeasonMetrics: (seasonYear) =>
    apiClient.get(`/ui/analytics/franchise-season/${seasonYear}`)};

export default AnalyticsApi;