import apiClient from "./apiClient";

const PicksApi = {
  submitPick: (pick) => apiClient.post("/ui/picks", pick),
  getUserPicksByWeek: (groupId, week) =>  apiClient.get(`/ui/picks/${groupId}/week/${week}`),
  getWidgetForUser: () => apiClient.get(`/ui/picks/2025/widget`),
  getWidgetForSynthetic: () => apiClient.get(`/ui/picks/2025/widget/synthetic`),
  getAccuracyChartForUser: () => apiClient.get(`/ui/picks/chart`),
  getAccuracyChartForSynthetic: () => apiClient.get(`/ui/picks/chart/synthetic`)
};

export default PicksApi;
