import apiClient from "./apiClient";

const PicksApi = {
  submitPick: (pick) => apiClient.post("/ui/picks", pick),
  getUserPicksByWeek: (groupId, week) =>  apiClient.get(`/ui/picks/${groupId}/week/${week}`),
  // StatBot advisor — docs/features/statbot-advisor.md. `level` is one of
  // Prevent | GoalLine | QbDraw | HailMary; omit it for StatBot's recommendation.
  getAdvice: (groupId, week, level) =>
    apiClient.get(`/ui/picks/${groupId}/week/${week}/advice`, {
      params: level ? { level } : undefined,
    }),
  getWidgetForUser: (seasonYear) => apiClient.get(`/ui/picks/${seasonYear}/widget`),
  getWidgetForSynthetic: (seasonYear) => apiClient.get(`/ui/picks/${seasonYear}/widget/synthetic`),
  getAccuracyChartForUser: () => apiClient.get(`/ui/picks/chart`),
  getAccuracyChartForSynthetic: () => apiClient.get(`/ui/picks/chart/synthetic`)
};

export default PicksApi;
